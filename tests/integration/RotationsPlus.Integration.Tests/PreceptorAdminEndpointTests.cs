using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Common;
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

        var list = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>("/api/preceptors?pageSize=100", JsonOptions);
        list!.Items.Select(p => p.Id).Should().NotContain(created.Id);
    }

    [Fact]
    public async Task List_paginates_and_searches_by_name()
    {
        var admin = Client(RoleNames.Admin);

        // Three preceptors sharing a unique surname token → a q on it returns exactly those three, deterministic.
        var token = $"Pagington{Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++)
        {
            (await PostAsync(admin, ValidCreate(firstName: $"P{i}", lastName: token))).EnsureSuccessStatusCode();
        }

        var page1 = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>(
            $"/api/preceptors?q={token}&page=1&pageSize=2", JsonOptions);
        var page2 = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>(
            $"/api/preceptors?q={token}&page=2&pageSize=2", JsonOptions);

        page1!.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page2!.Items.Should().HaveCount(1);
        page1.Items.Select(p => p.Id).Should().NotIntersectWith(page2.Items.Select(p => p.Id)); // no overlap
        page1.Items.Concat(page2.Items).Should().OnlyContain(p => p.FullName.Contains(token));
    }

    [Fact]
    public async Task List_search_matches_email()
    {
        var admin = Client(RoleNames.Admin);
        var email = UniqueEmail();
        var created = await (await PostAsync(admin, ValidCreate(email: email)))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        // Search by a distinctive fragment of the email.
        var frag = email.Split('@')[0];
        var found = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>(
            $"/api/preceptors?q={frag}&pageSize=100", JsonOptions);

        found!.Items.Should().ContainSingle(p => p.Id == created!.Id);
    }

    [Fact]
    public async Task List_search_matches_location()
    {
        var admin = Client(RoleNames.Admin);
        // A distinctive city so the location (city/state) ILIKE branch is what matches — not name/email.
        var city = $"Townsville{Guid.NewGuid():N}";
        var created = await (await PostAsync(admin, ValidCreate(city: city)))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var found = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>(
            $"/api/preceptors?q={city}&pageSize=100", JsonOptions);

        found!.Items.Should().ContainSingle(p => p.Id == created!.Id);
    }

    [Fact]
    public async Task List_search_over_the_length_limit_is_rejected()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.GetAsync($"/api/preceptors?q={new string('x', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Options_returns_the_unpaginated_picker_list_including_a_created_preceptor()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        // The options endpoint is for form pickers — it returns the full list, not a page.
        var options = await Client(RoleNames.Coordinator)
            .GetFromJsonAsync<List<PreceptorSummaryResponse>>("/api/preceptors/options", JsonOptions);

        options!.Should().Contain(p => p.Id == created!.Id);
    }

    [Fact]
    public async Task Customer_cannot_list_preceptor_options()
    {
        var student = Client(RoleNames.Student);

        var response = await student.GetAsync("/api/preceptors/options");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Approval queue (/admin/permission): batch Save (activate / reject checkboxes) ----

    private async Task<PreceptorDetailResponse> CreatePendingAsync(HttpClient admin) =>
        (await (await PostAsync(admin, ValidCreate(status: PreceptorStatus.Pending)))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions))!;

    private async Task<PreceptorDetailResponse> GetAsync(HttpClient admin, Guid id) =>
        (await admin.GetFromJsonAsync<PreceptorDetailResponse>($"/api/preceptors/{id}", JsonOptions))!;

    [Fact]
    public async Task Save_activates_the_checked_and_rejects_the_others_and_stamps_the_review()
    {
        var admin = Client(RoleNames.Admin);
        var toActivate = await CreatePendingAsync(admin);
        var toReject = await CreatePendingAsync(admin);

        var result = await (await admin.PostAsJsonAsync("/api/preceptors/permissions",
                new SavePreceptorPermissionsRequest([toActivate.Id], [toReject.Id]), JsonOptions))
            .Content.ReadFromJsonAsync<SavePreceptorPermissionsResponse>(JsonOptions);

        result!.Activated.Should().Be(1);
        result.Rejected.Should().Be(1);

        var activated = await GetAsync(admin, toActivate.Id);
        activated.Status.Should().Be(PreceptorStatus.MemberActivated);
        activated.ReviewedAtUtc.Should().NotBeNull(); // review stamped

        var rejected = await GetAsync(admin, toReject.Id);
        rejected.Status.Should().Be(PreceptorStatus.Rejected);
        rejected.ReviewedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_only_affects_pending_preceptors()
    {
        var admin = Client(RoleNames.Admin);
        // ValidCreate defaults to Registered (not Pending) → not in the queue; a Save must skip it.
        var registered = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var result = await (await admin.PostAsJsonAsync("/api/preceptors/permissions",
                new SavePreceptorPermissionsRequest([registered!.Id], []), JsonOptions))
            .Content.ReadFromJsonAsync<SavePreceptorPermissionsResponse>(JsonOptions);

        result!.Activated.Should().Be(0); // unchanged — not Pending
        (await GetAsync(admin, registered.Id)).Status.Should().Be(PreceptorStatus.Registered);
    }

    [Fact]
    public async Task Save_with_empty_lists_is_a_no_op_200()
    {
        var admin = Client(RoleNames.Admin);

        var result = await (await admin.PostAsJsonAsync("/api/preceptors/permissions",
                new SavePreceptorPermissionsRequest([], []), JsonOptions))
            .Content.ReadFromJsonAsync<SavePreceptorPermissionsResponse>(JsonOptions);

        result!.Activated.Should().Be(0);
        result.Rejected.Should().Be(0);
    }

    [Fact]
    public async Task Save_with_an_id_in_both_lists_is_400()
    {
        var admin = Client(RoleNames.Admin);
        var pending = await CreatePendingAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/preceptors/permissions",
            new SavePreceptorPermissionsRequest([pending.Id], [pending.Id]), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_pending_queue_returns_phone_and_scheduled()
    {
        // The seeded Pending preceptor (Nadia Khan) carries a phone + scheduled flag for the queue columns.
        var nadiaId = Guid.Parse("dddddddd-0000-0000-0000-000000000003");
        var admin = Client(RoleNames.Admin);

        var queue = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>(
            "/api/preceptors?status=Pending&pageSize=100", JsonOptions);

        var nadia = queue!.Items.FirstOrDefault(p => p.Id == nadiaId);
        nadia.Should().NotBeNull();
        nadia!.MobilePhone.Should().Be("+1 212-555-0103");
        nadia.CallScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task List_filtered_by_status_returns_only_that_status()
    {
        var admin = Client(RoleNames.Admin);
        var pending = await CreatePendingAsync(admin);
        var registered = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<PreceptorDetailResponse>(JsonOptions);

        var queue = await admin.GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>(
            "/api/preceptors?status=Pending&pageSize=100", JsonOptions);

        queue!.Items.Should().Contain(p => p.Id == pending.Id);
        queue.Items.Should().NotContain(p => p.Id == registered!.Id);
        queue.Items.Should().OnlyContain(p => p.Status == PreceptorStatus.Pending);
    }
}
