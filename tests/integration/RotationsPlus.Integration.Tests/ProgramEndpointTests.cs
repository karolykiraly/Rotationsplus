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
    // Seeded preceptors (see PreceptorConfiguration): Jane offers the two Internal Medicine
    // programs; Omar offers the single Pediatrics program.
    private static readonly Guid JaneCarterId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid OmarReyesId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

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
        // Seeded preceptor assignments surface in the list; the Family Medicine program is unassigned.
        programs!.Should().Contain(p => p.PreceptorName == "Jane Carter");
        programs!.Should().Contain(p => p.PreceptorName == null);
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
        program.PreceptorId.Should().Be(JaneCarterId);
        program.PreceptorName.Should().Be("Jane Carter");
    }

    [Fact]
    public async Task List_filtered_by_preceptor_returns_only_their_programs()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>(
            $"/api/programs?preceptorId={JaneCarterId}", JsonOptions);

        programs.Should().NotBeNull();
        programs!.Should().HaveCount(2); // both seeded Internal Medicine programs
        programs!.Should().OnlyContain(p => p.PreceptorName == "Jane Carter");
        programs!.Select(p => p.SpecialtyName).Should().OnlyContain(n => n == "Internal Medicine");
    }

    [Fact]
    public async Task List_filtered_by_another_preceptor_returns_only_their_program()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>(
            $"/api/programs?preceptorId={OmarReyesId}", JsonOptions);

        // Omar offers exactly the one seeded Pediatrics program — proves the filter discriminates.
        programs!.Should().ContainSingle().Which.PreceptorName.Should().Be("Omar Reyes");
    }

    [Fact]
    public async Task List_filtered_by_unknown_preceptor_returns_empty()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>(
            $"/api/programs?preceptorId={Guid.NewGuid()}", JsonOptions);

        programs.Should().NotBeNull();
        programs!.Should().BeEmpty();
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
