using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// Student self-booking: POST /api/customer/rotations creates a Pending rotation owned by the signed-in
/// student (resolved by CIAM oid). Runs against the seeded program; pairs with the deposit checkout flow.
/// </summary>
public class CustomerBookingEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded non-open program: 1500/wk, min 4 weeks.
    private static readonly Guid ProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly DateOnly FutureStart = new(2026, 11, 2);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Staff(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private HttpClient Customer(string oid, string role = RoleNames.Student)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static string UniqueOid() => $"ciam-{Guid.NewGuid():N}";

    /// <summary>Creates an oid-linked directory student and returns their oid (the booking caller).</summary>
    private async Task<string> CreateLinkedStudentAsync()
    {
        var oid = UniqueOid();
        var admin = Staff(RoleNames.Admin);
        await admin.PostAsJsonAsync("/api/students",
            new CreateStudentRequest("Book", "Student", $"book.{Guid.NewGuid():N}@example.com", null,
                AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions);
        return oid;
    }

    private static HttpContent Body(Guid programId, DateOnly start, int weeks) =>
        JsonContent.Create(new { programId, startDate = start, weeks }, options: JsonOptions);

    /// <summary>Reads the persisted rotation straight from the DB, so a test can assert the server-side
    /// ownership snapshot (StudentOid/Id) that the response intentionally doesn't expose.</summary>
    private async Task<Rotation?> GetBookingAsync(Guid rotationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.Rotations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rotationId);
    }

    [Fact]
    public async Task A_student_books_a_pending_rotation_owned_by_themselves()
    {
        var oid = await CreateLinkedStudentAsync();

        var response = await Customer(oid).PostAsync("/api/customer/rotations", Body(ProgramId, FutureStart, 4));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booked = await response.Content.ReadFromJsonAsync<CustomerRotationResponse>(JsonOptions);
        booked!.Status.Should().Be(RotationStatus.Pending);
        booked.Weeks.Should().Be(4);
        booked.StartDate.Should().Be(FutureStart);
        booked.EndDate.Should().Be(FutureStart.AddDays(28)); // 4 weeks
        booked.SpecialtyName.Should().NotBeNullOrEmpty();

        // The booking is scoped to the CALLER: the persisted row snapshots the caller's oid, not some
        // other student / a blank id. This is what would catch an ownership-resolution regression.
        var persisted = await GetBookingAsync(booked.Id);
        persisted!.StudentOid.Should().Be(oid);
        persisted.StudentName.Should().Be("Book Student");
    }

    [Fact]
    public async Task A_booked_rotation_shows_in_the_students_tracker_and_can_open_a_deposit()
    {
        var oid = await CreateLinkedStudentAsync();
        var customer = Customer(oid);

        var booked = await (await customer.PostAsync("/api/customer/rotations", Body(ProgramId, FutureStart, 4)))
            .Content.ReadFromJsonAsync<CustomerRotationResponse>(JsonOptions);

        // It appears in the student's own tracker…
        var mine = await customer.GetFromJsonAsync<CustomerRotationResponse[]>("/api/customer/rotations", JsonOptions);
        mine.Should().ContainSingle(r => r.Id == booked!.Id);

        // …and the deposit can be opened against it (the full find→book→pay chain).
        var deposit = await customer.PostAsync($"/api/rotations/{booked!.Id}/payment-intent", null);
        deposit.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Booking_below_the_program_minimum_weeks_is_rejected()
    {
        var oid = await CreateLinkedStudentAsync();

        var response = await Customer(oid).PostAsync("/api/customer/rotations", Body(ProgramId, FutureStart, 2));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); // program requires ≥ 4 weeks
    }

    [Fact]
    public async Task Booking_a_start_date_in_the_past_is_rejected()
    {
        var oid = await CreateLinkedStudentAsync();

        var response = await Customer(oid).PostAsync("/api/customer/rotations", Body(ProgramId, new DateOnly(2020, 1, 1), 4));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Booking_an_unknown_program_is_rejected()
    {
        var oid = await CreateLinkedStudentAsync();

        var response = await Customer(oid).PostAsync("/api/customer/rotations",
            Body(Guid.NewGuid(), FutureStart, 4));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_customer_without_a_linked_student_profile_cannot_book()
    {
        // A signed-in customer with no directory Student record (oid never linked).
        var response = await Customer(UniqueOid()).PostAsync("/api/customer/rotations", Body(ProgramId, FutureStart, 4));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Staff_cannot_self_book()
    {
        var response = await Staff(RoleNames.Admin).PostAsync("/api/customer/rotations", Body(ProgramId, FutureStart, 4));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_preceptor_customer_has_no_student_profile_and_cannot_book()
    {
        // "Customer" spans Student + Preceptor; a Preceptor passes CustomerOnly but owns no Student record,
        // so booking falls to the no-profile 400 rather than creating a rotation under the wrong identity.
        var response = await Customer(UniqueOid(), RoleNames.Preceptor)
            .PostAsync("/api/customer/rotations", Body(ProgramId, FutureStart, 4));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
