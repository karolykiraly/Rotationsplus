using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/customer/rotations — the signed-in student's "My rotations". Verifies the oid match
/// (caller ↔ their directory student), the student-hidden status filter, and the authz boundary.
/// </summary>
public class CustomerRotationEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

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

    /// <summary>Creates a directory student carrying <paramref name="oid"/>, returns its id.</summary>
    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Portal", "Student", $"portal.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId, RotationStatus status)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(InternalMedicineProgramId, studentId,
                    new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), status), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    [Fact]
    public async Task Student_sees_their_own_rotation_matched_by_oid()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId, RotationStatus.Active);

        var mine = await Customer(oid)
            .GetFromJsonAsync<List<CustomerRotationResponse>>("/api/customer/rotations", JsonOptions);

        var row = mine!.SingleOrDefault(r => r.Id == rotationId);
        row.Should().NotBeNull();
        row!.SpecialtyName.Should().Be("Internal Medicine");
        row.PreceptorName.Should().Be("Jane Carter");
        row.Status.Should().Be(RotationStatus.Active);
    }

    [Fact]
    public async Task A_student_does_not_see_another_students_rotation()
    {
        var admin = Staff(RoleNames.Admin);
        var ownerOid = UniqueOid();
        var ownerStudentId = await CreateStudentAsync(admin, ownerOid);
        var rotationId = await CreateRotationAsync(admin, ownerStudentId, RotationStatus.Active);

        // A different signed-in student must not see it.
        var others = await Customer(UniqueOid())
            .GetFromJsonAsync<List<CustomerRotationResponse>>("/api/customer/rotations", JsonOptions);

        others!.Select(r => r.Id).Should().NotContain(rotationId);
    }

    [Fact]
    public async Task Hidden_status_rotations_are_excluded_from_the_tracker()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var visibleId = await CreateRotationAsync(admin, studentId, RotationStatus.Completed);
        var hiddenId = await CreateRotationAsync(admin, studentId, RotationStatus.Cancelled);

        var mine = await Customer(oid)
            .GetFromJsonAsync<List<CustomerRotationResponse>>("/api/customer/rotations", JsonOptions);

        mine!.Select(r => r.Id).Should().Contain(visibleId);
        mine!.Select(r => r.Id).Should().NotContain(hiddenId);   // Cancelled is hidden (Plan_Student §7)
    }

    [Fact]
    public async Task Staff_are_rejected_with_403()
    {
        var response = await Staff(RoleNames.Coordinator).GetAsync("/api/customer/rotations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/customer/rotations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
