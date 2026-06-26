using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Common;
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

    private HttpClient StudentClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-student");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);
        return client;
    }

    [Fact]
    public async Task Customer_can_browse_the_program_catalog()
    {
        // The student-facing portal reads the catalog with a CIAM (Student) token.
        var programs = await StudentClient()
            .GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs/catalog?q=internal", JsonOptions);

        programs.Should().NotBeNull();
        programs!.Should().OnlyContain(p => p.SpecialtyName == "Internal Medicine");
    }

    [Fact]
    public async Task Customer_program_detail_hides_the_honorarium_but_shows_retail()
    {
        var program = await StudentClient()
            .GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{SeededInternalMedicineInPerson}", JsonOptions);

        program.Should().NotBeNull();
        program!.RetailAmountPerWeek.Should().Be(1500m);   // what the student pays — visible
        program.WeeklyHonorarium.Should().BeNull();         // preceptor pay / margin — hidden from customers
    }

    // The catalog (full filtered list) backs the customer browse + form picker; its filters are
    // specialtyId/preceptorId/programType/maxRetailPerWeek and a q over specialty + description.
    private Task<List<ProgramSummaryResponse>?> Search(string queryString) =>
        StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>($"/api/programs/catalog?{queryString}", JsonOptions);

    [Fact]
    public async Task Catalog_returns_seeded_programs_with_specialty_names()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs/catalog", JsonOptions);

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
    public async Task Get_by_id_returns_the_catalog_fields()
    {
        var program = await StaffClient().GetFromJsonAsync<ProgramDetailResponse>($"/api/programs/{SeededInternalMedicineInPerson}", JsonOptions);

        program!.ProgramNumber.Should().Be(1001);   // seeded sequential number → client formats as "IP1001"
        program.City.Should().Be("Los Angeles");
        program.State.Should().Be("CA");
        program.Tags.Should().Contain("Hospital Letterhead LOR").And.Contain("Inpatient");
    }

    [Fact]
    public async Task Catalog_carries_the_catalog_fields_and_open_flag()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs/catalog", JsonOptions);

        // Program numbers are present and unique across the catalog.
        programs!.Should().OnlyContain(p => p.ProgramNumber > 0);
        programs!.Select(p => p.ProgramNumber).Should().OnlyHaveUniqueItems();
        // The seeded tele-rotation is the "open" (instant-approval) one; the InPerson IM program isn't.
        programs!.Should().Contain(p => p.ProgramType == ProgramType.TeleRotation && p.IsOpen);
        programs!.Should().Contain(p => p.City == "Los Angeles" && p.State == "CA");
    }

    [Fact]
    public async Task Catalog_filtered_by_preceptor_returns_only_their_programs()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>(
            $"/api/programs/catalog?preceptorId={JaneCarterId}", JsonOptions);

        programs.Should().NotBeNull();
        programs!.Should().HaveCount(2); // both seeded Internal Medicine programs
        programs!.Should().OnlyContain(p => p.PreceptorName == "Jane Carter");
        programs!.Select(p => p.SpecialtyName).Should().OnlyContain(n => n == "Internal Medicine");
    }

    [Fact]
    public async Task Catalog_filtered_by_another_preceptor_returns_only_their_program()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>(
            $"/api/programs/catalog?preceptorId={OmarReyesId}", JsonOptions);

        // Omar offers exactly the one seeded Pediatrics program — proves the filter discriminates.
        programs!.Should().ContainSingle().Which.PreceptorName.Should().Be("Omar Reyes");
    }

    [Fact]
    public async Task Catalog_filtered_by_unknown_preceptor_returns_empty()
    {
        var programs = await StaffClient().GetFromJsonAsync<List<ProgramSummaryResponse>>(
            $"/api/programs/catalog?preceptorId={Guid.NewGuid()}", JsonOptions);

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

    // ---- Paged admin list (/api/programs): program-type tabs + name search over specialty/preceptor ----

    [Fact]
    public async Task Paged_list_returns_a_page_and_the_full_total()
    {
        var page = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?pageSize=2", JsonOptions);

        page.Should().NotBeNull();
        page!.TotalCount.Should().Be(4);           // four seeded programs
        page.Items.Should().HaveCount(2);          // one page
        page.Items.Select(p => p.SpecialtyName).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Paged_list_pages_are_disjoint_and_cover_the_set()
    {
        var page1 = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?page=1&pageSize=2", JsonOptions);
        var page2 = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?page=2&pageSize=2", JsonOptions);

        page1!.Items.Should().HaveCount(2);
        page2!.Items.Should().HaveCount(2);
        page1.Items.Select(p => p.Id).Should().NotIntersectWith(page2.Items.Select(p => p.Id));
        page1.Items.Concat(page2.Items).Select(p => p.Id).Should().OnlyHaveUniqueItems().And.HaveCount(4);
    }

    [Fact]
    public async Task Paged_list_filters_by_a_single_program_type_tab()
    {
        var page = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?programType=InPerson&pageSize=100", JsonOptions);

        page!.TotalCount.Should().Be(2); // Internal Medicine InPerson + Pediatrics InPerson
        page.Items.Should().OnlyContain(p => p.ProgramType == ProgramType.InPerson);
    }

    [Fact]
    public async Task Paged_list_filters_by_multiple_program_types_for_one_tab()
    {
        // The Consultation tab spans two enum values; the multi-valued programType is OR within the filter.
        var page = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?programType=InPerson&programType=TeleRotation&pageSize=100", JsonOptions);

        page!.Items.Should().OnlyContain(p =>
            p.ProgramType == ProgramType.InPerson || p.ProgramType == ProgramType.TeleRotation);
        page.Items.Should().Contain(p => p.ProgramType == ProgramType.TeleRotation);
    }

    [Fact]
    public async Task Paged_list_search_matches_preceptor_name()
    {
        // The admin list searches specialty + preceptor name (NOT description — that's the catalog).
        // "Carter" is Jane's surname; it matches her programs and nothing by description.
        var page = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?q=Carter&pageSize=100", JsonOptions);

        page!.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(p => p.PreceptorName == "Jane Carter");
    }

    [Fact]
    public async Task Paged_list_q_too_long_returns_400()
    {
        var response = await StaffClient().GetAsync($"/api/programs?q={new string('a', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Paged_list_q_wildcard_is_treated_literally()
    {
        // %25 is URL-encoded '%' (an ILIKE wildcard). Escaped, it matches a literal '%' (none seeded),
        // so the page is empty rather than "everything" — proving the shared escaper is applied here too.
        var page = await StaffClient().GetFromJsonAsync<PagedResponse<ProgramSummaryResponse>>(
            "/api/programs?q=%25&pageSize=100", JsonOptions);

        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
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
    public async Task Catalog_q_too_long_returns_400()
    {
        var response = await StaffClient().GetAsync($"/api/programs/catalog?q={new string('a', 101)}");

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
