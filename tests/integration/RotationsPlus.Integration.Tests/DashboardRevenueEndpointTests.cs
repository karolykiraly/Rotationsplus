using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Payments;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/dashboard/revenue — the admin Revenue tab. Verifies a captured deposit lands in collected /
/// outstanding / by-type / this-month, that a refund moves the money from collected to refunded (not
/// double-counted), and the AdminOnly boundary.
/// </summary>
public class DashboardRevenueEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Program cccccccc-0001 is non-open (10% deposit) and InPerson.
    private static readonly Guid ProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Staff(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private HttpClient Customer(string oid)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);
        return client;
    }

    private static string UniqueOid() => $"ciam-{Guid.NewGuid():N}";

    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Rev", "Student", $"rev.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(ProgramId, studentId,
                    new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 30), RotationStatus.Pending), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    /// <summary>Books a rotation and captures its deposit via the dev simulator. Returns the rotation id
    /// and the captured payment.</summary>
    private async Task<(Guid rotationId, Payment payment)> PaidRotationAsync()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);

        var customer = Customer(oid);
        var paymentId = (await (await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null))
            .Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var payment = await db.Payments.AsNoTracking().FirstAsync(p => p.RotationId == rotationId);
        return (rotationId, payment);
    }

    private async Task SetRotationStatusAsync(Guid rotationId, RotationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var rotation = await db.Rotations.FirstAsync(r => r.Id == rotationId);
        rotation.Status = status;
        await db.SaveChangesAsync();
    }

    private Task<DashboardRevenueResponse> RevenueAsync() =>
        Staff(RoleNames.Admin).GetFromJsonAsync<DashboardRevenueResponse>("/api/dashboard/revenue", JsonOptions)!;

    [Fact]
    public async Task A_captured_deposit_lands_in_collected_outstanding_by_type_and_this_month()
    {
        var before = await RevenueAsync();
        var (_, payment) = await PaidRotationAsync();
        var after = await RevenueAsync();

        after.Currency.Should().Be("USD");
        after.Collected.Should().Be(before.Collected + payment.Amount);
        after.OutstandingReceivable.Should().Be(before.OutstandingReceivable + payment.OutstandingAmount);
        // The deposit was captured just now → in the current business month.
        after.CollectedThisMonth.Should().Be(before.CollectedThisMonth + payment.Amount);
        // The latest month of the trend (current month) reflects the capture too.
        after.MonthlyTrend.Should().NotBeEmpty();
        after.MonthlyTrend[^1].Amount.Should().Be(before.MonthlyTrend[^1].Amount + payment.Amount);
        // Program cccccccc-0001 is InPerson → its by-type bucket grew by the deposit.
        BucketFor(after, ProgramType.InPerson).Should().Be(BucketFor(before, ProgramType.InPerson) + payment.Amount);
        // The per-type breakdown always sums to the headline collected total.
        after.ByProgramType.Sum(t => t.Amount).Should().Be(after.Collected);
    }

    [Fact]
    public async Task A_refund_moves_money_from_collected_to_refunded_without_double_counting()
    {
        var (rotationId, payment) = await PaidRotationAsync();
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled);

        var before = await RevenueAsync();
        (await Staff(RoleNames.Admin).PostAsync($"/api/rotations/{rotationId}/refund", null)).EnsureSuccessStatusCode();
        var after = await RevenueAsync();

        // Refunding removes the deposit from collected and adds it to refunded — never subtracted twice.
        after.Collected.Should().Be(before.Collected - payment.Amount);
        after.Refunded.Should().Be(before.Refunded + payment.Amount);
        // A refunded booking is no longer an outstanding receivable.
        after.OutstandingReceivable.Should().Be(before.OutstandingReceivable - payment.OutstandingAmount);
        after.ByProgramType.Sum(t => t.Amount).Should().Be(after.Collected);
    }

    [Fact]
    public async Task Non_admin_staff_are_rejected_with_403()
    {
        var response = await Staff(RoleNames.Sales).GetAsync("/api/dashboard/revenue");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/dashboard/revenue");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static decimal BucketFor(DashboardRevenueResponse r, ProgramType type) =>
        r.ByProgramType.Where(t => t.Type == type).Sum(t => t.Amount);
}
