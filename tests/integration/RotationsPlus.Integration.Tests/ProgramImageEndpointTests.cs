using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Common;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// Program image upload/delete. The integration host has no storage connection configured, so the
/// in-memory image store stands in — upload + serve still work end-to-end and the program's
/// <c>ImageUrl</c> becomes a real (non-null) value, which is what these tests assert.
/// </summary>
public class ProgramImageEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private async Task<Guid> CreateProgramAsync(HttpClient admin)
    {
        var body = new CreateProgramRequest(InternalMedicineId, ProgramType.InPerson, 2, 4, 1500m, 500m, "Imageable offering.", null);
        var created = await (await admin.PostAsJsonAsync("/api/programs", body, JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string contentType, string fileName = "hospital.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        // The minimal-API IFormFile parameter is named "file" → the form field must match.
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    // A valid PNG magic number (89 50 4E 47 0D 0A 1A 0A) + padding to clear the 12-byte sniff window.
    // The endpoint authenticates the image by these leading bytes, not the declared content type.
    private static readonly byte[] FakePng =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    [Fact]
    public async Task Admin_uploads_an_image_and_it_appears_on_the_program_detail()
    {
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);

        // Before upload there is no image.
        var before = await admin.GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{id}", JsonOptions);
        before!.ImageUrl.Should().BeNull();

        var upload = await admin.PostAsync($"/api/programs/{id}/image", ImageForm(FakePng, "image/png"));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await upload.Content.ReadFromJsonAsync<ProgramImageResponse>(JsonOptions);
        result!.ImageUrl.Should().NotBeNullOrWhiteSpace();

        // The image now surfaces on a fresh detail read and on the catalog list.
        var after = await admin.GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{id}", JsonOptions);
        after!.ImageUrl.Should().NotBeNullOrWhiteSpace();

        // The signed read URL (not the raw blob name) surfaces on the catalog list — proving the rewrite ran.
        var list = await admin.GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs/catalog", JsonOptions);
        list!.Single(p => p.Id == id).ImageUrl.Should().Contain("images.local/");

        // The paged admin list signs the read URL on the page too (a separate rewrite path from /catalog).
        var paged = await admin.GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>("/api/programs?pageSize=100", JsonOptions);
        paged!.Items.Single(p => p.Id == id).ImageUrl.Should().Contain("images.local/");
    }

    [Fact]
    public async Task Upload_rejects_a_non_image_content_type()
    {
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);

        var bytes = Encoding.UTF8.GetBytes("not an image at all");
        var response = await admin.PostAsync($"/api/programs/{id}/image", ImageForm(bytes, "text/plain", "evil.txt"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_rejects_non_image_bytes_mislabelled_as_an_image()
    {
        // The attack the magic-byte check defends against: declare image/png but send a (polyglot/HTML)
        // payload. Content type is image/png yet the bytes aren't a real image → must be rejected.
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);

        var bytes = Encoding.UTF8.GetBytes("<svg onload=alert(1)>not really a png</svg>");
        var response = await admin.PostAsync($"/api/programs/{id}/image", ImageForm(bytes, "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_with_no_file_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);

        var response = await admin.PostAsync($"/api/programs/{id}/image", new MultipartFormDataContent());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_to_an_unknown_program_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.PostAsync($"/api/programs/{Guid.NewGuid()}/image", ImageForm(FakePng, "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_image_from_the_detail()
    {
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);
        (await admin.PostAsync($"/api/programs/{id}/image", ImageForm(FakePng, "image/png"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await admin.DeleteAsync($"/api/programs/{id}/image");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await admin.GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{id}", JsonOptions);
        after!.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Delete_with_no_image_is_idempotent()
    {
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);

        var delete = await admin.DeleteAsync($"/api/programs/{id}/image");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Non_admin_cannot_upload()
    {
        var admin = Client(RoleNames.Admin);
        var id = await CreateProgramAsync(admin);

        var sales = Client(RoleNames.Sales);
        var response = await sales.PostAsync($"/api/programs/{id}/image", ImageForm(FakePng, "image/png"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
