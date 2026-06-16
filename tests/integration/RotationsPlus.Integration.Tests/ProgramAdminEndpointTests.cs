using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

public class ProgramAdminEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded specialties (see SpecialtyConfiguration) — valid FK targets for created programs.
    private static readonly Guid InternalMedicineId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid PediatricsId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007");

    // The API serializes/parses enums as strings; match that on the wire.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static CreateProgramRequest ValidCreate(
        Guid? specialtyId = null,
        ProgramType type = ProgramType.InPerson,
        int maxStudents = 2,
        int minWeeks = 4,
        decimal retail = 1500m,
        decimal honorarium = 500m,
        string? description = "A new rotation offering.") =>
        new(specialtyId ?? InternalMedicineId, type, maxStudents, minWeeks, retail, honorarium, description);

    private Task<HttpResponseMessage> PostAsync(HttpClient client, CreateProgramRequest body) =>
        client.PostAsJsonAsync("/api/programs", body, JsonOptions);

    [Fact]
    public async Task Admin_can_create_then_fetch_a_program()
    {
        var admin = Client(RoleNames.Admin);

        var create = await PostAsync(admin, ValidCreate(
            type: ProgramType.TeleResearch, maxStudents: 6, minWeeks: 3,
            retail: 1234.50m, honorarium: 321.00m, description: "A new rotation offering."));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.SpecialtyId.Should().Be(InternalMedicineId);
        created.SpecialtyName.Should().Be("Internal Medicine");
        created.ProgramType.Should().Be(ProgramType.TeleResearch);
        created.MaxStudentsPerRotation.Should().Be(6);
        created.MinWeeksPerRotation.Should().Be(3);
        created.RetailAmountPerWeek.Should().Be(1234.50m);
        created.WeeklyHonorarium.Should().Be(321.00m);
        created.Description.Should().Be("A new rotation offering.");

        // Every field round-trips through a fresh read (not just the create response).
        var fetched = await admin.GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{created.Id}", JsonOptions);
        fetched!.SpecialtyName.Should().Be("Internal Medicine");
        fetched.ProgramType.Should().Be(ProgramType.TeleResearch);
        fetched.MaxStudentsPerRotation.Should().Be(6);
        fetched.MinWeeksPerRotation.Should().Be(3);
        fetched.RetailAmountPerWeek.Should().Be(1234.50m);
        fetched.WeeklyHonorarium.Should().Be(321.00m);
        fetched.Description.Should().Be("A new rotation offering.");
    }

    [Fact]
    public async Task Create_trims_description_whitespace()
    {
        var admin = Client(RoleNames.Admin);

        var create = await PostAsync(admin, ValidCreate(description: "  Trimmed offering.  "));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        created!.Description.Should().Be("Trimmed offering.");
    }

    [Fact]
    public async Task Create_with_unknown_specialty_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(specialtyId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_negative_retail_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(retail: -1m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_zero_capacity_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(maxStudents: 0));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_negative_honorarium_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(honorarium: -0.01m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_zero_min_weeks_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(minWeeks: 0));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_money_over_the_column_ceiling_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        // 100,000,000.00 exceeds numeric(10,2) — must be rejected cleanly, not 500 on save.
        var response = await PostAsync(admin, ValidCreate(retail: 100_000_000.00m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_sub_cent_pricing_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        // More than 2 decimal places would be silently rounded by the column — reject instead,
        // so the create response always matches the persisted value.
        var response = await PostAsync(admin, ValidCreate(retail: 1234.567m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_oversized_description_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(description: new string('x', 4001)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_referencing_a_soft_deleted_specialty_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh specialty we then delete — isolated from the seeded data other tests rely on.
        var specialty = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Transient Specialty")))
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
    public async Task Admin_can_update_a_program()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(retail: 1000m)))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);

        var update = new UpdateProgramRequest(
            InternalMedicineId, ProgramType.Consultation, 5, 3, 2000m, 750m, "Updated offering.");
        var response = await admin.PutAsJsonAsync($"/api/programs/{created!.Id}", update, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{created.Id}", JsonOptions);
        fetched!.ProgramType.Should().Be(ProgramType.Consultation);
        fetched.MaxStudentsPerRotation.Should().Be(5);
        fetched.RetailAmountPerWeek.Should().Be(2000m);
        fetched.WeeklyHonorarium.Should().Be(750m);
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var update = new UpdateProgramRequest(InternalMedicineId, ProgramType.InPerson, 2, 4, 1500m, 500m, null);
        var response = await admin.PutAsJsonAsync($"/api/programs/{Guid.NewGuid()}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_can_change_the_specialty()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);

        var update = new UpdateProgramRequest(PediatricsId, ProgramType.InPerson, 2, 4, 1500m, 500m, null);
        var response = await admin.PutAsJsonAsync($"/api/programs/{created!.Id}", update, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{created.Id}", JsonOptions);
        fetched!.SpecialtyId.Should().Be(PediatricsId);
        fetched.SpecialtyName.Should().Be("Pediatrics");
    }

    [Fact]
    public async Task Update_with_negative_honorarium_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);

        var update = new UpdateProgramRequest(InternalMedicineId, ProgramType.InPerson, 2, 4, 1500m, -5m, null);
        var response = await admin.PutAsJsonAsync($"/api/programs/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_oversized_description_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);

        var update = new UpdateProgramRequest(InternalMedicineId, ProgramType.InPerson, 2, 4, 1500m, 500m, new string('x', 4001));
        var response = await admin.PutAsJsonAsync($"/api/programs/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_unknown_specialty_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);

        // Valid program id but a non-existent specialty — validation/FK check yields 400, not 404.
        var update = new UpdateProgramRequest(Guid.NewGuid(), ProgramType.InPerson, 2, 4, 1500m, 500m, null);
        var response = await admin.PutAsJsonAsync($"/api/programs/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_soft_delete_a_program_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(description: "Soon to be deleted.")))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/programs/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/programs/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await admin.GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs", JsonOptions);
        list!.Select(p => p.Id).Should().NotContain(created.Id);
    }
}
