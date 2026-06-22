using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// PHASE 2g-1: required-document materialization on booking, the per-rotation document checklist, and
/// the computed "Documents" tracker state. The seeded Internal-Medicine program requires 4 documents,
/// so a rotation booked on it materializes 4 UploadNeeded docs → the tracker shows Missing.
/// </summary>
public class DocumentEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
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

    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Doc", "Student", $"doc.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(InternalMedicineProgramId, studentId,
                    new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.NotStarted), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    [Fact]
    public async Task Booking_on_a_program_with_required_docs_materializes_an_upload_needed_checklist()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);

        var docs = await Customer(oid).GetFromJsonAsync<List<RotationDocumentResponse>>(
            $"/api/customer/rotations/{rotationId}/documents", JsonOptions);

        // The seeded IM program requires 4 documents; all start UploadNeeded with no file yet.
        docs!.Should().HaveCount(4);
        docs.Should().OnlyContain(d => d.Status == DocumentStatus.UploadNeeded);
        docs.Should().OnlyContain(d => d.FileName == null);
        docs!.Select(d => d.DocumentTypeName).Should().Contain("Curriculum Vitae (CV)");
        // Due 14 days before the 2026-09-07 start.
        docs.Should().OnlyContain(d => d.DueDate == new DateOnly(2026, 8, 24));
    }

    [Fact]
    public async Task The_tracker_documents_state_is_missing_when_docs_are_outstanding()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);

        var mine = await Customer(oid)
            .GetFromJsonAsync<List<CustomerRotationResponse>>("/api/customer/rotations", JsonOptions);

        mine!.Single(r => r.Id == rotationId).DocumentsState.Should().Be(RotationDocumentsState.Missing);
    }

    [Fact]
    public async Task A_student_cannot_read_another_students_rotation_documents()
    {
        var admin = Staff(RoleNames.Admin);
        var ownerOid = UniqueOid();
        var ownerStudentId = await CreateStudentAsync(admin, ownerOid);
        var rotationId = await CreateRotationAsync(admin, ownerStudentId);

        // A different signed-in student gets an (indistinguishable) empty list, not the owner's docs.
        var docs = await Customer(UniqueOid()).GetFromJsonAsync<List<RotationDocumentResponse>>(
            $"/api/customer/rotations/{rotationId}/documents", JsonOptions);

        docs!.Should().BeEmpty();
    }

    [Fact]
    public async Task Self_booking_a_required_docs_program_reports_missing_and_lists_the_docs()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        await CreateStudentAsync(admin, oid);
        var customer = Customer(oid);

        var booked = await (await customer.PostAsJsonAsync("/api/customer/rotations",
                new CustomerBookingRequest(InternalMedicineProgramId, new DateOnly(2026, 11, 2), 4), JsonOptions))
            .Content.ReadFromJsonAsync<CustomerRotationResponse>(JsonOptions);

        booked!.DocumentsState.Should().Be(RotationDocumentsState.Missing);

        var docs = await customer.GetFromJsonAsync<List<RotationDocumentResponse>>(
            $"/api/customer/rotations/{booked.Id}/documents", JsonOptions);
        docs!.Should().HaveCount(4).And.OnlyContain(d => d.Status == DocumentStatus.UploadNeeded);
    }

    [Fact]
    public async Task Staff_are_rejected_from_the_customer_documents_endpoint_with_403()
    {
        var response = await Staff(RoleNames.Coordinator)
            .GetAsync($"/api/customer/rotations/{Guid.NewGuid()}/documents");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_from_the_customer_documents_endpoint_with_401()
    {
        var response = await factory.CreateClient()
            .GetAsync($"/api/customer/rotations/{Guid.NewGuid()}/documents");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
