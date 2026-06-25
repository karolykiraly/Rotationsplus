using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

public class SpecialtyAdminEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    [Fact]
    public async Task Admin_can_create_then_fetch_a_specialty()
    {
        var admin = Client(RoleNames.Admin);

        var create = await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("  Sports Medicine  "));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<SpecialtyResponse>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Sports Medicine"); // trimmed

        var fetched = await admin.GetFromJsonAsync<SpecialtyResponse>($"/api/specialties/{created.Id}");
        fetched!.Name.Should().Be("Sports Medicine");
    }

    [Fact]
    public async Task Create_with_duplicate_name_returns_409()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Internal Medicine"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_with_blank_name_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_admin_staff_cannot_create()
    {
        var sales = Client(RoleNames.Sales);

        var response = await sales.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Nephrology"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_rename_a_specialty()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Urolgy")))
            .Content.ReadFromJsonAsync<SpecialtyResponse>();

        var update = await admin.PutAsJsonAsync($"/api/specialties/{created!.Id}", new UpdateSpecialtyRequest("Urology"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<SpecialtyResponse>($"/api/specialties/{created.Id}");
        fetched!.Name.Should().Be("Urology");
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.PutAsJsonAsync($"/api/specialties/{Guid.NewGuid()}", new UpdateSpecialtyRequest("Whatever"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_can_soft_delete_a_specialty_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Toxicology")))
            .Content.ReadFromJsonAsync<SpecialtyResponse>();

        var delete = await admin.DeleteAsync($"/api/specialties/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/specialties/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await admin.GetFromJsonAsync<List<SpecialtyResponse>>("/api/specialties");
        list!.Select(s => s.Name).Should().NotContain("Toxicology");
    }

    [Fact]
    public async Task Deleting_a_specialty_a_live_program_uses_returns_409()
    {
        var admin = Client(RoleNames.Admin);
        var specialty = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Allergy & Immunology")))
            .Content.ReadFromJsonAsync<SpecialtyResponse>();

        // A program now references the specialty → it can't be deleted out from under it.
        var program = await admin.PostAsJsonAsync("/api/programs",
            new CreateProgramRequest(specialty!.Id, ProgramType.InPerson, 2, 4, 500m, 100m, "Guard test", PreceptorId: null));
        program.StatusCode.Should().Be(HttpStatusCode.Created);

        var delete = await admin.DeleteAsync($"/api/specialties/{specialty.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Still present after the rejected delete.
        (await admin.GetAsync($"/api/specialties/{specialty.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deleting_a_specialty_a_live_preceptor_uses_returns_409()
    {
        var admin = Client(RoleNames.Admin);
        var specialty = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Neonatology")))
            .Content.ReadFromJsonAsync<SpecialtyResponse>();

        // A preceptor's PrimarySpecialtyId is the second FK to Specialty — also blocks the delete.
        var preceptor = await admin.PostAsJsonAsync("/api/preceptors",
            new CreatePreceptorRequest("Dana", "Lin", $"dana.{Guid.NewGuid():N}@example.com", specialty!.Id,
                null, null, null, null, PreceptorStatus.Registered, null));
        preceptor.StatusCode.Should().Be(HttpStatusCode.Created);

        var delete = await admin.DeleteAsync($"/api/specialties/{specialty.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Recreating_a_soft_deleted_specialty_restores_it()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Hepatology")))
            .Content.ReadFromJsonAsync<SpecialtyResponse>();
        await admin.DeleteAsync($"/api/specialties/{created!.Id}");

        var recreate = await admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Hepatology"));
        recreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var restored = await recreate.Content.ReadFromJsonAsync<SpecialtyResponse>();
        restored!.Id.Should().Be(created.Id); // same row, undeleted
    }
}
