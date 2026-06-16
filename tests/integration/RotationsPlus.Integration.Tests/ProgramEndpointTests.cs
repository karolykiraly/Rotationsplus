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
    private static readonly Guid InternalMedicineId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

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

    private Task<List<ProgramSummaryResponse>?> Search(string queryString) =>
        StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>($"/api/programs?{queryString}", JsonOptions);

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

    [Fact]
    public async Task Filter_by_specialty_returns_only_that_specialty()
    {
        var programs = await Search($"specialtyId={InternalMedicineId}");

        programs!.Should().HaveCount(2);
        programs!.Should().OnlyContain(p => p.SpecialtyName == "Internal Medicine");
    }

    [Fact]
    public async Task Filter_by_program_type_returns_only_that_type()
    {
        var programs = await Search("programType=InPerson");

        programs!.Should().HaveCount(2); // Internal Medicine InPerson + Pediatrics InPerson
        programs!.Should().OnlyContain(p => p.ProgramType == ProgramType.InPerson);
    }

    [Fact]
    public async Task Filter_by_max_retail_returns_only_within_budget()
    {
        var programs = await Search("maxRetailPerWeek=1000");

        programs!.Should().HaveCount(2); // the $1000 tele-rotation and the $900 consultation
        programs!.Should().OnlyContain(p => p.RetailAmountPerWeek <= 1000m);
    }

    [Fact]
    public async Task Search_q_matches_specialty_or_description()
    {
        var programs = await Search("q=pediatric");

        programs!.Should().ContainSingle().Which.SpecialtyName.Should().Be("Pediatrics");
    }

    [Fact]
    public async Task Search_q_is_case_insensitive()
    {
        var programs = await Search("q=PEDIATRIC");

        programs!.Should().ContainSingle().Which.SpecialtyName.Should().Be("Pediatrics");
    }

    [Fact]
    public async Task Combined_filters_and_together()
    {
        var programs = await Search($"specialtyId={InternalMedicineId}&programType=TeleRotation");

        programs!.Should().ContainSingle().Which.ProgramType.Should().Be(ProgramType.TeleRotation);
    }

    [Fact]
    public async Task Search_with_no_match_returns_empty()
    {
        var programs = await Search("q=zzzznotarealprogram");

        programs.Should().NotBeNull();
        programs!.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_q_wildcard_is_treated_literally()
    {
        // %25 is URL-encoded '%' — an ILIKE wildcard. Escaped, it matches a literal '%' (none seeded),
        // so the result is empty rather than "everything".
        var programs = await Search("q=%25");

        programs.Should().NotBeNull();
        programs!.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_q_too_long_returns_400()
    {
        var response = await StaffClient().GetAsync($"/api/programs?q={new string('a', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_q_matches_description_only()
    {
        // "hands-on" appears only in the Pediatrics program's description, not in any specialty name.
        var programs = await Search("q=hands-on");

        programs!.Should().ContainSingle().Which.SpecialtyName.Should().Be("Pediatrics");
    }

    [Fact]
    public async Task Filter_by_preceptor_and_type_ands_together()
    {
        var programs = await Search($"preceptorId={JaneCarterId}&programType=TeleRotation");

        programs!.Should().ContainSingle().Which.ProgramType.Should().Be(ProgramType.TeleRotation);
    }

    [Fact]
    public async Task Filter_by_max_retail_is_inclusive_at_the_boundary()
    {
        // At 999 the $1000 tele-rotation is excluded, leaving only the $900 consultation.
        var programs = await Search("maxRetailPerWeek=999");

        programs!.Should().ContainSingle().Which.RetailAmountPerWeek.Should().Be(900m);
    }
}
