using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

public class SpecialtyEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private const string SeededInternalMedicineId = "aaaaaaaa-0000-0000-0000-000000000001";

    private HttpClient StaffClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-staff");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Admin);
        return client;
    }

    [Fact]
    public async Task List_returns_the_seeded_specialties()
    {
        var specialties = await StaffClient().GetFromJsonAsync<List<SpecialtyResponse>>("/api/specialties");

        specialties.Should().NotBeNull();
        specialties!.Should().HaveCount(15);
        specialties!.Select(s => s.Name).Should().Contain("Internal Medicine");
        // Ordered by name.
        specialties!.Select(s => s.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Get_by_id_returns_the_specialty()
    {
        var specialty = await StaffClient().GetFromJsonAsync<SpecialtyResponse>($"/api/specialties/{SeededInternalMedicineId}");

        specialty.Should().NotBeNull();
        specialty!.Id.Should().Be(Guid.Parse(SeededInternalMedicineId));
        specialty.Name.Should().Be("Internal Medicine");
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404()
    {
        var response = await StaffClient().GetAsync($"/api/specialties/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_without_auth_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/specialties");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
