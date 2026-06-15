using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

public class ProgramEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private const string SeededInternalMedicineInPerson = "cccccccc-0000-0000-0000-000000000001";

    // The API serializes enums as strings; match that when deserializing.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient StaffClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-staff");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Coordinator);
        return client;
    }

    [Fact]
    public async Task List_returns_seeded_programs_with_specialty_names()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs", JsonOptions);

        programs.Should().NotBeNull();
        programs!.Should().HaveCount(4);
        programs!.Select(p => p.SpecialtyName).Should().Contain("Internal Medicine");
        programs!.Should().OnlyContain(p => p.RetailAmountPerWeek > 0);
        // Ordered by specialty name.
        programs!.Select(p => p.SpecialtyName).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Get_by_id_returns_full_detail()
    {
        var program = await StaffClient().GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{SeededInternalMedicineInPerson}", JsonOptions);

        program.Should().NotBeNull();
        program!.SpecialtyName.Should().Be("Internal Medicine");
        program.ProgramType.Should().Be(ProgramType.InPerson);
        program.MaxStudentsPerRotation.Should().Be(2);
        program.MinWeeksPerRotation.Should().Be(4);
        program.RetailAmountPerWeek.Should().Be(1500m);
        program.WeeklyHonorarium.Should().Be(500m);
        program.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404()
    {
        var response = await StaffClient().GetAsync($"/api/programs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_without_auth_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/programs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
