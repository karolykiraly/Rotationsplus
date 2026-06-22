using System.Net;
using System.Net.Http.Headers;
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
/// PHASE 2g-3a: admin document review — list a student's documents, set status (the review dropdown),
/// upload/replace on behalf, and clear a file. Backed by the in-memory document store.
/// </summary>
public class AdminDocumentEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly byte[] Pdf = "%PDF-1.4\n%abc\n"u8.ToArray();

    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-admin");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Admin);
        return client;
    }

    private HttpClient Staff(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static string UniqueOid() => $"ciam-{Guid.NewGuid():N}";

    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Adm", "Review", $"adm.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task CreateRotationAsync(HttpClient admin, Guid studentId)
    {
        await admin.PostAsJsonAsync("/api/rotations",
            new CreateRotationRequest(InternalMedicineProgramId, studentId,
                new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.NotStarted), JsonOptions);
    }

    private async Task<(Guid studentId, List<AdminRotationDocumentResponse> docs)> SeedStudentWithDocsAsync()
    {
        var admin = Admin();
        var studentId = await CreateStudentAsync(admin, UniqueOid());
        await CreateRotationAsync(admin, studentId);
        var docs = await admin.GetFromJsonAsync<List<AdminRotationDocumentResponse>>(
            $"/api/students/{studentId}/documents", JsonOptions);
        return (studentId, docs!);
    }

    [Fact]
    public async Task Admin_lists_a_students_documents_with_rotation_context()
    {
        var (_, docs) = await SeedStudentWithDocsAsync();

        docs.Should().HaveCount(4); // the seeded IM program requires 4
        docs.Should().OnlyContain(d => d.RotationNumber > 0);
        docs.Should().OnlyContain(d => d.Status == DocumentStatus.UploadNeeded);
    }

    [Fact]
    public async Task Admin_sets_a_document_to_rejected_with_a_reason_then_approved()
    {
        var admin = Admin();
        var (_, docs) = await SeedStudentWithDocsAsync();
        var docId = docs[0].Id;

        var rejected = await (await admin.PutAsJsonAsync($"/api/documents/{docId}/status",
                new SetDocumentStatusRequest(DocumentStatus.Rejected, "Illegible scan"), JsonOptions))
            .Content.ReadFromJsonAsync<AdminRotationDocumentResponse>(JsonOptions);
        rejected!.Status.Should().Be(DocumentStatus.Rejected);
        rejected.RejectionReason.Should().Be("Illegible scan");
        rejected.ReviewedAtUtc.Should().NotBeNull();

        var approved = await (await admin.PutAsJsonAsync($"/api/documents/{docId}/status",
                new SetDocumentStatusRequest(DocumentStatus.Approved, null), JsonOptions))
            .Content.ReadFromJsonAsync<AdminRotationDocumentResponse>(JsonOptions);
        approved!.Status.Should().Be(DocumentStatus.Approved);
        approved.RejectionReason.Should().BeNull(); // cleared when not rejected
    }

    [Fact]
    public async Task Admin_uploads_on_behalf_then_clears_the_file()
    {
        var admin = Admin();
        var (_, docs) = await SeedStudentWithDocsAsync();
        var docId = docs[0].Id;

        var content = new ByteArrayContent(Pdf);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { content, "file", "onbehalf.pdf" } };

        var uploaded = await (await admin.PostAsync($"/api/documents/{docId}/file", form))
            .Content.ReadFromJsonAsync<AdminRotationDocumentResponse>(JsonOptions);
        uploaded!.Status.Should().Be(DocumentStatus.Submitted);
        uploaded.FileName.Should().Be("onbehalf.pdf");
        uploaded.FileUrl.Should().NotBeNullOrEmpty();

        var cleared = await (await admin.DeleteAsync($"/api/documents/{docId}/file"))
            .Content.ReadFromJsonAsync<AdminRotationDocumentResponse>(JsonOptions);
        cleared!.Status.Should().Be(DocumentStatus.UploadNeeded);
        cleared.FileName.Should().BeNull();
        cleared.FileUrl.Should().BeNull();
    }

    [Fact]
    public async Task Non_admin_staff_are_rejected_with_403()
    {
        var response = await Staff(RoleNames.Coordinator)
            .GetAsync($"/api/students/{Guid.NewGuid()}/documents");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
