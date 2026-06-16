using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

public class PreceptorAdminEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded data (see SpecialtyConfiguration / PreceptorConfiguration).
    private static readonly Guid InternalMedicineId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid PediatricsId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007");
    private const string SeededJaneEmail = "jane.carter@example.com";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    // A unique email per call keeps creates isolated in the shared DB.
    private static string UniqueEmail() => $"test.{Guid.NewGuid():N}@example.com";

    private static CreatePreceptorRequest ValidCreate(
        string firstName = "Test",
        string lastName = "Preceptor",
        string? email = null,
        Guid? specialtyId = null,
        string? license = "MD-12345",
        string? licenseState = "CA",
        string? city = "San Diego",
        string? state = "CA",
        PreceptorStatus status = PreceptorStatus.Registered,
        string? bio = "Experienced clinician.") =>
        new(firstName, lastName, email ?? UniqueEmail(), specialtyId ?? InternalMedicineId,
            license, licenseState, city, state, status, bio);

    private Task<HttpResponseMessage> PostAsync(HttpClient client, CreatePreceptorRequest body) =>
        client.PostAsJsonAsync("/api/preceptors", body, JsonOptions);

    [Fact]
    public async Task Admin_can_create_then_fetch_a_preceptor()
    {
        var admin = Client(RoleNames.Admin);

        var create = await PostAsync(admin, ValidCreate(
            firstName: "Alice", lastName: "Nguyen", specialtyId: PediatricsId,
            status: PreceptorStatus.MemberActivated, city: "Boston", state: "MA", bio: "Pediatrician."));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Alice");
        created.LastName.Should().Be("Nguyen");
        created.PrimarySpecialtyId.Should().Be(PediatricsId);
        created.PrimarySpecialtyName.Should().Be("Pediatrics");
        created.Status.Should().Be(PreceptorStatus.MemberActivated);
        created.City.Should().Be("Boston");
        created.Bio.Should().Be("Pediatrician.");

        var fetched = await admin.GetFromJsonAsync<PreceptorDetailResponse>($"/api/preceptors/{created.Id}", JsonOptions);
        fetched!.FirstName.Should().Be("Alice");
        fetched.PrimarySpecialtyName.Should().Be("Pediatrics");
        fetched.Status.Should().Be(PreceptorStatus.MemberActivated);
    }

    [Fact]
    public async Task Create_trims_name_whitespace()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(firstName: "  Margaret  ")))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        created!.FirstName.Should().Be("Margaret");
    }

    [Fact]
    public async Task Create_with_unknown_specialty_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(specialtyId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_blank_first_name_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(firstName: "   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_invalid_email_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(email: "not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_duplicate_email_returns_409()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(email: SeededJaneEmail));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_with_oversized_bio_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(bio: new string('x', 4001)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_referencing_a_soft_deleted_specialty_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var specialty = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Preceptor FK Specialty")))
            .Content.ReadFromJsonAsync<SpecialtyResponse>();
        (await admin.DeleteAsync($"/api/specialties/{specialty!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await PostAsync(admin, ValidCreate(specialtyId: specialty.Id));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_admin_staff_cannot_create()
    {
        var sales = Client(RoleNames.Sales);

        var response = await PostAsync(sales, ValidCreate());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Recreating_a_soft_deleted_preceptor_restores_and_refreshes_it()
    {
        var admin = Client(RoleNames.Admin);
        var email = UniqueEmail();
        var created = await (await PostAsync(admin, ValidCreate(firstName: "Original", email: email)))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);
        await admin.DeleteAsync($"/api/preceptors/{created!.Id}");

        var recreate = await PostAsync(admin, ValidCreate(firstName: "Refreshed", email: email));
        recreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var restored = await recreate.Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);
        restored!.Id.Should().Be(created.Id);        // same row, undeleted
        restored.FirstName.Should().Be("Refreshed"); // and refreshed with the new data
    }

    [Fact]
    public async Task Admin_can_update_a_preceptor()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var update = new UpdatePreceptorRequest(
            "Updated", "Name", created!.Email, PediatricsId, "MD-999", "NY", "Albany", "NY",
            PreceptorStatus.MemberSigned, "Updated bio.");
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{created.Id}", update, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Every mutable field round-trips through a fresh read.
        var fetched = await admin.GetFromJsonAsync<PreceptorDetailResponse>($"/api/preceptors/{created.Id}", JsonOptions);
        fetched!.FirstName.Should().Be("Updated");
        fetched.LastName.Should().Be("Name");
        fetched.PrimarySpecialtyId.Should().Be(PediatricsId);
        fetched.PrimarySpecialtyName.Should().Be("Pediatrics");
        fetched.MedicalLicenseNumber.Should().Be("MD-999");
        fetched.LicenseState.Should().Be("NY");
        fetched.City.Should().Be("Albany");
        fetched.State.Should().Be("NY");
        fetched.Status.Should().Be(PreceptorStatus.MemberSigned);
        fetched.Bio.Should().Be("Updated bio.");
    }

    [Fact]
    public async Task Update_with_blank_first_name_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var update = new UpdatePreceptorRequest("   ", "Name", created!.Email, InternalMedicineId, null, null, null, null, PreceptorStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{created.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_invalid_email_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var update = new UpdatePreceptorRequest("A", "B", "not-an-email", InternalMedicineId, null, null, null, null, PreceptorStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_oversized_bio_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var update = new UpdatePreceptorRequest("A", "B", created!.Email, InternalMedicineId, null, null, null, null, PreceptorStatus.Registered, new string('x', 4001));
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{created.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_to_email_of_a_soft_deleted_preceptor_returns_409()
    {
        var admin = Client(RoleNames.Admin);

        // A preceptor we soft-delete — its email stays reserved by the (unfiltered) unique index.
        var deletedEmail = UniqueEmail();
        var toDelete = await (await PostAsync(admin, ValidCreate(email: deletedEmail)))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);
        (await admin.DeleteAsync($"/api/preceptors/{toDelete!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A live preceptor that tries to take the soft-deleted one's email.
        var live = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);
        var update = new UpdatePreceptorRequest("A", "B", deletedEmail, InternalMedicineId, null, null, null, null, PreceptorStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{live!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var update = new UpdatePreceptorRequest("A", "B", UniqueEmail(), InternalMedicineId, null, null, null, null, PreceptorStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{Guid.NewGuid()}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_to_an_existing_email_returns_409()
    {
        var admin = Client(RoleNames.Admin);
        var first = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);
        var second = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        // Try to take the first preceptor's email on the second.
        var update = new UpdatePreceptorRequest("X", "Y", first!.Email, InternalMedicineId, null, null, null, null, PreceptorStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{second!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_with_unknown_specialty_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var update = new UpdatePreceptorRequest("A", "B", created!.Email, Guid.NewGuid(), null, null, null, null, PreceptorStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/preceptors/{created.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_soft_delete_a_preceptor_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/preceptors/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/preceptors/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await admin.GetFromJsonAsync<List<PreceptorSummaryResponse>>("/api/preceptors", JsonOptions);
        list!.Select(p => p.Id).Should().NotContain(created.Id);
    }
}
