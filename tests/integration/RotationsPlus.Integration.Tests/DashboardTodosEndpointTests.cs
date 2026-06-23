using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/dashboard/todos — the admin "ToDo's" tab. Verifies each actionable queue surfaces the
/// right rows (a submitted document, a Pending rotation awaiting payment, a Pending preceptor) with a
/// count that matches, plus the AdminOnly boundary.
/// </summary>
public class DashboardTodosEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid InternalMedicineSpecialtyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly byte[] Pdf = "%PDF-1.4\n%abc\n"u8.ToArray();

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
                new CreateStudentRequest("Todo", "Student", $"todo.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, $"ciam-{Guid.NewGuid():N}"), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId, RotationStatus status)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(InternalMedicineProgramId, studentId,
                    new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 29), status), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    [Fact]
    public async Task Submitting_a_document_surfaces_it_in_the_review_queue()
    {
        var admin = Client(RoleNames.Admin);
        var studentId = await CreateStudentAsync(admin);
        await CreateRotationAsync(admin, studentId, RotationStatus.NotStarted);

        var before = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);

        // Upload on behalf flips one materialized document UploadNeeded → Submitted (awaiting review).
        var docs = await admin.GetFromJsonAsync<List<AdminRotationDocumentResponse>>(
            $"/api/students/{studentId}/documents", JsonOptions);
        var content = new ByteArrayContent(Pdf);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { content, "file", "cv.pdf" } };
        (await admin.PostAsync($"/api/documents/{docs![0].Id}/file", form)).EnsureSuccessStatusCode();

        var after = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);

        after!.DocumentsToReview.Count.Should().Be(before!.DocumentsToReview.Count + 1);
        after.DocumentsToReview.Items.Should().Contain(d => d.DocumentId == docs[0].Id && d.StudentId == studentId);
    }

    [Fact]
    public async Task A_submitted_document_on_a_soft_deleted_rotation_is_excluded()
    {
        var admin = Client(RoleNames.Admin);
        var studentId = await CreateStudentAsync(admin);
        var rotationId = await CreateRotationAsync(admin, studentId, RotationStatus.NotStarted);

        // Submit a document, then soft-delete the rotation (which does NOT cascade to its documents).
        var docs = await admin.GetFromJsonAsync<List<AdminRotationDocumentResponse>>(
            $"/api/students/{studentId}/documents", JsonOptions);
        var content = new ByteArrayContent(Pdf);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { content, "file", "cv.pdf" } };
        (await admin.PostAsync($"/api/documents/{docs![0].Id}/file", form)).EnsureSuccessStatusCode();

        var before = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);
        (await admin.DeleteAsync($"/api/rotations/{rotationId}")).EnsureSuccessStatusCode();
        var after = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);

        // The dangling document drops out of both the count and the preview (no NULL-nav row).
        after!.DocumentsToReview.Count.Should().Be(before!.DocumentsToReview.Count - 1);
        after.DocumentsToReview.Items.Should().NotContain(d => d.DocumentId == docs[0].Id);
    }

    [Fact]
    public async Task A_pending_rotation_appears_in_the_awaiting_payment_queue()
    {
        var admin = Client(RoleNames.Admin);
        var studentId = await CreateStudentAsync(admin);

        var before = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);
        var rotationId = await CreateRotationAsync(admin, studentId, RotationStatus.Pending);
        var after = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);

        after!.AwaitingPayment.Count.Should().Be(before!.AwaitingPayment.Count + 1);
        after.AwaitingPayment.Items.Should().Contain(p => p.RotationId == rotationId);
    }

    [Fact]
    public async Task A_pending_preceptor_appears_in_the_approval_queue()
    {
        var admin = Client(RoleNames.Admin);

        var before = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);

        var email = $"prec.{Guid.NewGuid():N}@example.com";
        var created = await (await admin.PostAsJsonAsync("/api/preceptors",
                new CreatePreceptorRequest("Pat", "Pending", email, InternalMedicineSpecialtyId,
                    null, null, null, null, PreceptorStatus.Pending, null), JsonOptions))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var after = await admin.GetFromJsonAsync<DashboardTodosResponse>("/api/dashboard/todos", JsonOptions);

        after!.PreceptorApprovals.Count.Should().Be(before!.PreceptorApprovals.Count + 1);
        after.PreceptorApprovals.Items.Should().Contain(p => p.PreceptorId == created!.Id && p.FullName == "Pat Pending");
    }

    [Fact]
    public async Task Non_admin_staff_are_rejected_with_403()
    {
        var response = await Client(RoleNames.Sales).GetAsync("/api/dashboard/todos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/dashboard/todos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
