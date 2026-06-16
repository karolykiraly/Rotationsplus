using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Integration.Tests;

public class RotationEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded program (see ProgramConfiguration) — Internal Medicine / InPerson / Jane Carter.
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    // Seeded specialty (see SpecialtyConfiguration) — a valid FK target for a freshly created program.
    private static readonly Guid InternalMedicineSpecialtyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    // Seeded rotation (see RotationConfiguration) — "Sam Rivera", Active.
    private static readonly Guid SeededRotationId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

    // The API serializes/parses enums (RotationStatus, ProgramType) and DateOnly as the seeded format.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static CreateRotationRequest ValidCreate(
        Guid? programId = null,
        string name = "Pat Morgan",
        string email = "pat.morgan@example.com",
        string? oid = null,
        DateOnly? start = null,
        DateOnly? end = null,
        RotationStatus status = RotationStatus.Pending) =>
        new(
            programId ?? InternalMedicineProgramId,
            name,
            email,
            oid,
            start ?? new DateOnly(2026, 9, 7),
            end ?? new DateOnly(2026, 10, 5),   // 28 days → 4 weeks
            status);

    private Task<HttpResponseMessage> PostAsync(HttpClient client, CreateRotationRequest body) =>
        client.PostAsJsonAsync("/api/rotations", body, JsonOptions);

    [Fact]
    public async Task Admin_list_includes_the_seeded_rotation_with_program_flattened()
    {
        var admin = Client(RoleNames.Admin);

        var list = await admin.GetFromJsonAsync<List<RotationSummaryResponse>>("/api/rotations", JsonOptions);

        var seeded = list!.SingleOrDefault(r => r.Id == SeededRotationId);
        seeded.Should().NotBeNull();
        seeded!.StudentName.Should().Be("Sam Rivera");
        seeded.SpecialtyName.Should().Be("Internal Medicine");
        seeded.ProgramType.Should().Be(ProgramType.InPerson);
        seeded.PreceptorName.Should().Be("Jane Carter");
        seeded.Status.Should().Be(RotationStatus.Active);
    }

    [Fact]
    public async Task Admin_can_get_the_seeded_rotation_by_id()
    {
        var admin = Client(RoleNames.Admin);

        var rotation = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{SeededRotationId}", JsonOptions);

        rotation!.Id.Should().Be(SeededRotationId);
        rotation.ProgramId.Should().Be(InternalMedicineProgramId);
        rotation.StudentName.Should().Be("Sam Rivera");
        rotation.SpecialtyName.Should().Be("Internal Medicine");
        rotation.PreceptorName.Should().Be("Jane Carter");
        rotation.Weeks.Should().Be(4);
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.GetAsync($"/api/rotations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_can_create_a_rotation_and_weeks_is_derived_from_the_date_range()
    {
        var admin = Client(RoleNames.Admin);

        var create = await PostAsync(admin, ValidCreate(
            name: "  Jordan Lee  ", email: "  jordan.lee@example.com  ", oid: "  ciam-oid-123  ",
            start: new DateOnly(2026, 11, 2), end: new DateOnly(2026, 11, 30), status: RotationStatus.NotStarted));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.ProgramId.Should().Be(InternalMedicineProgramId);
        created.SpecialtyName.Should().Be("Internal Medicine");
        created.ProgramType.Should().Be(ProgramType.InPerson);
        created.PreceptorName.Should().Be("Jane Carter");
        created.StudentName.Should().Be("Jordan Lee");        // trimmed
        created.StudentEmail.Should().Be("jordan.lee@example.com");
        created.StudentOid.Should().Be("ciam-oid-123");
        created.Weeks.Should().Be(4);                         // 28 days, derived server-side
        created.Status.Should().Be(RotationStatus.NotStarted);

        // Round-trips through a fresh read.
        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{created.Id}", JsonOptions);
        fetched!.StudentName.Should().Be("Jordan Lee");
        fetched.Weeks.Should().Be(4);
    }

    [Fact]
    public async Task Create_omitting_the_oid_leaves_it_null()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(oid: null)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        created!.StudentOid.Should().BeNull();
    }

    [Fact]
    public async Task Create_with_unknown_program_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(programId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_end_on_or_before_start_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(
            start: new DateOnly(2026, 9, 7), end: new DateOnly(2026, 9, 7)));

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
    public async Task Create_with_blank_name_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(name: "   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_update_a_rotation_status_and_dates()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(status: RotationStatus.Pending)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, "Pat Morgan", "pat.morgan@example.com", "linked-oid",
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 19), RotationStatus.Active);   // 42 days → 6 weeks
        var response = await admin.PutAsJsonAsync($"/api/rotations/{created!.Id}", update, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{created.Id}", JsonOptions);
        fetched!.Status.Should().Be(RotationStatus.Active);
        fetched.StudentOid.Should().Be("linked-oid");
        fetched.Weeks.Should().Be(6);   // re-derived from the new range
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, "Pat Morgan", "pat.morgan@example.com", null,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending);
        var response = await admin.PutAsJsonAsync($"/api/rotations/{Guid.NewGuid()}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_can_soft_delete_a_rotation_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(name: "Soon Deleted")))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/rotations/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/rotations/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await admin.GetFromJsonAsync<List<RotationSummaryResponse>>("/api/rotations", JsonOptions);
        list!.Select(r => r.Id).Should().NotContain(created.Id);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh Rejected rotation, isolated from the seeded Active one.
        var created = await (await PostAsync(admin, ValidCreate(name: "Filter Me", status: RotationStatus.Rejected)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var rejected = await admin.GetFromJsonAsync<List<RotationSummaryResponse>>(
            "/api/rotations?status=Rejected", JsonOptions);
        rejected!.Select(r => r.Id).Should().Contain(created!.Id);
        rejected.Should().OnlyContain(r => r.Status == RotationStatus.Rejected);
    }

    [Fact]
    public async Task List_filters_by_program()
    {
        var admin = Client(RoleNames.Admin);

        var byProgram = await admin.GetFromJsonAsync<List<RotationSummaryResponse>>(
            $"/api/rotations?programId={InternalMedicineProgramId}", JsonOptions);

        byProgram!.Should().Contain(r => r.Id == SeededRotationId);
        // Every returned row belongs to that program (the seeded rotation does).
        byProgram.Should().OnlyContain(r => r.SpecialtyName == "Internal Medicine");
    }

    [Fact]
    public async Task Create_rounds_a_partial_week_up()
    {
        var admin = Client(RoleNames.Admin);

        // 30 days (Sep 7 → Oct 7) is 4 whole weeks + 2 days → must report 5, not truncate to 4.
        var created = await (await PostAsync(admin, ValidCreate(
                start: new DateOnly(2026, 9, 7), end: new DateOnly(2026, 10, 7))))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        created!.Weeks.Should().Be(5);
    }

    [Fact]
    public async Task Update_with_invalid_email_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, "Pat Morgan", "not-an-email", null,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending);
        var response = await admin.PutAsJsonAsync($"/api/rotations/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_unknown_program_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var update = new UpdateRotationRequest(
            Guid.NewGuid(), "Pat Morgan", "pat.morgan@example.com", null,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending);
        var response = await admin.PutAsJsonAsync($"/api/rotations/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_a_program_with_a_rotation_is_blocked_and_the_list_still_loads()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh program + a rotation booked against it, isolated from the seeded data.
        var program = await (await admin.PostAsJsonAsync("/api/programs",
                new CreateProgramRequest(InternalMedicineSpecialtyId, ProgramType.InPerson, 2, 4, 1500m, 500m, "Deletable?", null), JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        var rotation = await (await PostAsync(admin, ValidCreate(programId: program!.Id, name: "Orphan Risk")))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        // The program can't be deleted out from under its rotation.
        var delete = await admin.DeleteAsync($"/api/programs/{program.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // And the admin rotations list still loads (the regression this guard prevents: a soft-deleted
        // program would make the list project a NULL ProgramType through the navigation and 500).
        var list = await admin.GetAsync("/api/rotations");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await list.Content.ReadFromJsonAsync<List<RotationSummaryResponse>>(JsonOptions);
        rows!.Should().Contain(r => r.Id == rotation!.Id);

        // Once the rotation is gone, the program is deletable again.
        (await admin.DeleteAsync($"/api/rotations/{rotation!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/programs/{program.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Non_admin_staff_cannot_list()
    {
        var sales = Client(RoleNames.Sales);

        var response = await sales.GetAsync("/api/rotations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
