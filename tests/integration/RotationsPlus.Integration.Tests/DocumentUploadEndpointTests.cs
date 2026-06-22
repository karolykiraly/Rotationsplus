using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// PHASE 2g-2a: the student document upload (POST .../documents/{id}/file). Verifies magic-byte
/// validation, the UploadNeeded→Submitted transition + minted read URL, re-upload replacement,
/// ownership, and the authz boundary. Backed by the in-memory document store (no Azure needed).
/// </summary>
public class DocumentUploadEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // Minimal valid magic-byte payloads.
    private static readonly byte[] Pdf = "%PDF-1.4\n%abc\n"u8.ToArray();
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] NotAFile = "this is plain text, not a document"u8.ToArray();

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
                new CreateStudentRequest("Up", "Loader", $"up.{Guid.NewGuid():N}@example.com", null,
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

    private async Task<List<RotationDocumentResponse>> DocsAsync(HttpClient customer, Guid rotationId) =>
        (await customer.GetFromJsonAsync<List<RotationDocumentResponse>>(
            $"/api/customer/rotations/{rotationId}/documents", JsonOptions))!;

    private static MultipartFormDataContent FilePart(byte[] bytes, string fileName, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { content, "file", fileName } };
    }

    [Fact]
    public async Task Uploading_a_pdf_marks_the_document_submitted_with_a_read_url()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);
        var customer = Customer(oid);

        var docId = (await DocsAsync(customer, rotationId)).First(d => d.Status == DocumentStatus.UploadNeeded).Id;

        var response = await customer.PostAsync(
            $"/api/customer/rotations/{rotationId}/documents/{docId}/file",
            FilePart(Pdf, "immunization.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<RotationDocumentResponse>(JsonOptions);
        updated!.Status.Should().Be(DocumentStatus.Submitted);
        updated.FileName.Should().Be("immunization.pdf");
        updated.FileUrl.Should().NotBeNullOrEmpty();
        updated.SubmittedAtUtc.Should().NotBeNull();

        // The checklist now reflects the submission.
        var after = await DocsAsync(customer, rotationId);
        after.Single(d => d.Id == docId).Status.Should().Be(DocumentStatus.Submitted);
        after.Single(d => d.Id == docId).FileUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Re_uploading_replaces_the_file_and_keeps_it_submitted()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);
        var customer = Customer(oid);
        var docId = (await DocsAsync(customer, rotationId)).First(d => d.Status == DocumentStatus.UploadNeeded).Id;

        await customer.PostAsync($"/api/customer/rotations/{rotationId}/documents/{docId}/file",
            FilePart(Pdf, "first.pdf", "application/pdf"));
        var second = await customer.PostAsync($"/api/customer/rotations/{rotationId}/documents/{docId}/file",
            FilePart(Jpeg, "photo.jpg", "image/jpeg"));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await second.Content.ReadFromJsonAsync<RotationDocumentResponse>(JsonOptions);
        updated!.FileName.Should().Be("photo.jpg");
        updated.Status.Should().Be(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task A_file_whose_bytes_are_not_pdf_jpeg_or_png_is_rejected_400()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);
        var customer = Customer(oid);
        var docId = (await DocsAsync(customer, rotationId)).First().Id;

        // Mislabel plain text as a PDF — the magic-byte sniff rejects it.
        var response = await customer.PostAsync($"/api/customer/rotations/{rotationId}/documents/{docId}/file",
            FilePart(NotAFile, "fake.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_student_cannot_upload_to_another_students_document_404()
    {
        var admin = Staff(RoleNames.Admin);
        var ownerOid = UniqueOid();
        var ownerStudentId = await CreateStudentAsync(admin, ownerOid);
        var rotationId = await CreateRotationAsync(admin, ownerStudentId);
        var docId = (await DocsAsync(Customer(ownerOid), rotationId)).First().Id;

        var response = await Customer(UniqueOid()).PostAsync(
            $"/api/customer/rotations/{rotationId}/documents/{docId}/file",
            FilePart(Pdf, "x.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_approved_document_cannot_be_replaced_409()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);
        var customer = Customer(oid);
        var docId = (await DocsAsync(customer, rotationId)).First(d => d.Status == DocumentStatus.UploadNeeded).Id;

        // Force the document to Approved directly (the admin approve path is PHASE 2g-3).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
            await db.RotationDocuments.Where(d => d.Id == docId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DocumentStatus.Approved));
        }

        var response = await customer.PostAsync(
            $"/api/customer/rotations/{rotationId}/documents/{docId}/file",
            FilePart(Pdf, "again.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Staff_are_rejected_from_upload_with_403()
    {
        var response = await Staff(RoleNames.Coordinator).PostAsync(
            $"/api/customer/rotations/{Guid.NewGuid()}/documents/{Guid.NewGuid()}/file",
            FilePart(Pdf, "x.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
