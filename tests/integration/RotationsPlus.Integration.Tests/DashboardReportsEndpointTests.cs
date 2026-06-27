using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/dashboard/reports — the admin Reports tab. Verifies the totals, the registration trend
/// (a student registered today lands in the current month), the conversion funnel + top-specialties
/// (a booked rotation moves the student into "with booking" and bumps its specialty), and AdminOnly.
/// </summary>
public class DashboardReportsEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid ProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001"); // Internal Medicine

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private async Task<Guid> CreateStudentAsync(HttpClient admin)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Rep", "Student", $"rep.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, $"ciam-{Guid.NewGuid():N}"), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task CreateRotationAsync(HttpClient admin, Guid studentId)
    {
        (await admin.PostAsJsonAsync("/api/rotations",
            new CreateRotationRequest(ProgramId, studentId,
                new DateOnly(2027, 4, 5), new DateOnly(2027, 5, 3), RotationStatus.NotStarted), JsonOptions))
            .EnsureSuccessStatusCode();
    }

    private Task<DashboardReportsResponse> ReportsAsync() =>
        Client(RoleNames.Admin).GetFromJsonAsync<DashboardReportsResponse>("/api/dashboard/reports", JsonOptions)!;

    [Fact]
    public async Task Returns_totals_and_a_six_month_registration_trend()
    {
        var reports = await ReportsAsync();

        reports.TotalStudents.Should().BeGreaterThanOrEqualTo(2);
        reports.TotalRotations.Should().BeGreaterThanOrEqualTo(1);
        reports.StudentsWithBooking.Should().BeLessThanOrEqualTo(reports.TotalStudents);
        reports.Registrations.Should().HaveCount(6);
        // Project to a sortable key first — BeInAscendingOrder's selector overload only accepts a
        // member path, not a computed expression (mirrors the upcoming-starts assertion pattern).
        reports.Registrations.Select(r => r.Year * 100 + r.Month).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task A_student_registered_today_lands_in_the_current_month_bucket()
    {
        var admin = Client(RoleNames.Admin);
        var before = await ReportsAsync();
        await CreateStudentAsync(admin);
        var after = await ReportsAsync();

        after.TotalStudents.Should().Be(before.TotalStudents + 1);
        // The latest month bucket (current business month) gained a student.
        after.Registrations[^1].Students.Should().Be(before.Registrations[^1].Students + 1);
    }

    [Fact]
    public async Task Booking_a_rotation_counts_the_student_as_converted_and_bumps_its_specialty()
    {
        var admin = Client(RoleNames.Admin);
        var before = await ReportsAsync();
        var imBefore = SpecialtyCount(before, "Internal Medicine");

        var studentId = await CreateStudentAsync(admin);
        await CreateRotationAsync(admin, studentId);

        var after = await ReportsAsync();
        after.StudentsWithBooking.Should().Be(before.StudentsWithBooking + 1);
        after.TotalRotations.Should().Be(before.TotalRotations + 1);
        SpecialtyCount(after, "Internal Medicine").Should().Be(imBefore + 1);
    }

    [Fact]
    public async Task Non_admin_staff_are_rejected_with_403()
    {
        var response = await Client(RoleNames.Sales).GetAsync("/api/dashboard/reports");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/dashboard/reports");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static int SpecialtyCount(DashboardReportsResponse r, string specialty) =>
        r.TopSpecialties.Where(s => s.SpecialtyName == specialty).Sum(s => s.RotationCount);
}
