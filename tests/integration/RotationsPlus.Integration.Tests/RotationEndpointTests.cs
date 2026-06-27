using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Common;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

public class RotationEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded program (see ProgramConfiguration) — Internal Medicine / InPerson / Jane Carter.
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    // Seeded specialty (see SpecialtyConfiguration) — a valid FK target for a freshly created program.
    private static readonly Guid InternalMedicineSpecialtyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    // Seeded rotation (see RotationConfiguration) — "Sam Rivera", Active, linked to the Sam Rivera student.
    private static readonly Guid SeededRotationId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    // Seeded students (see StudentConfiguration).
    private static readonly Guid SamRiveraStudentId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    private static readonly Guid DanaColeStudentId = Guid.Parse("ffffffff-0000-0000-0000-000000000002");

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
        Guid? studentId = null,
        DateOnly? start = null,
        DateOnly? end = null,
        RotationStatus status = RotationStatus.Pending) =>
        new(
            programId ?? InternalMedicineProgramId,
            studentId ?? SamRiveraStudentId,
            start ?? new DateOnly(2026, 9, 7),
            end ?? new DateOnly(2026, 10, 5),   // 28 days → 4 weeks
            status);

    private Task<HttpResponseMessage> PostAsync(HttpClient client, CreateRotationRequest body) =>
        client.PostAsJsonAsync("/api/rotations", body, JsonOptions);

    [Fact]
    public async Task Admin_list_includes_the_seeded_rotation_with_program_flattened()
    {
        var admin = Client(RoleNames.Admin);

        // Narrow to the seeded rotation's number so the assertion is deterministic regardless of how many
        // other rotations the shared test DB holds (a plain page-1 scan could push it off as the suite grows).
        var list = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=R1001&pageSize=100", JsonOptions);

        var seeded = list!.Items.SingleOrDefault(r => r.Id == SeededRotationId);
        seeded.Should().NotBeNull();
        seeded!.RotationNumber.Should().Be(1001);   // seeded sequential number → client formats as "R1001"
        seeded.StudentName.Should().Be("Sam Rivera");
        seeded.SpecialtyName.Should().Be("Internal Medicine");
        seeded.ProgramType.Should().Be(ProgramType.InPerson);
        seeded.PreceptorName.Should().Be("Jane Carter");
        seeded.Status.Should().Be(RotationStatus.Active);
    }

    [Fact]
    public async Task Admin_can_get_the_seeded_rotation_by_id_with_its_student_link()
    {
        var admin = Client(RoleNames.Admin);

        var rotation = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{SeededRotationId}", JsonOptions);

        rotation!.Id.Should().Be(SeededRotationId);
        rotation.RotationNumber.Should().Be(1001);
        rotation.ProgramId.Should().Be(InternalMedicineProgramId);
        rotation.StudentId.Should().Be(SamRiveraStudentId);   // linked to the directory record
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
    public async Task Creating_a_rotation_longer_than_the_week_cap_is_rejected()
    {
        var admin = Client(RoleNames.Admin);

        // A fat-fingered far-future end date (~11 years → >520 weeks) would overflow the per-week money
        // columns (deposit price + honorarium schedule) at fulfilment; the cap rejects it up front.
        var response = await PostAsync(admin, ValidCreate(
            start: new DateOnly(2026, 9, 7), end: new DateOnly(2037, 9, 7)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_create_a_rotation_snapshotting_the_student_and_deriving_weeks()
    {
        var admin = Client(RoleNames.Admin);

        var create = await PostAsync(admin, ValidCreate(
            studentId: DanaColeStudentId,
            start: new DateOnly(2026, 11, 2), end: new DateOnly(2026, 11, 30), status: RotationStatus.NotStarted));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.ProgramId.Should().Be(InternalMedicineProgramId);
        created.RotationNumber.Should().BeGreaterThan(1001);   // server-assigned above the seeded number
        created.SpecialtyName.Should().Be("Internal Medicine");
        created.PreceptorName.Should().Be("Jane Carter");
        created.StudentId.Should().Be(DanaColeStudentId);
        created.StudentName.Should().Be("Dana Cole");                 // snapshotted from the directory student
        created.StudentEmail.Should().Be("dana.cole@example.com");
        created.Weeks.Should().Be(4);                                 // 28 days, derived server-side
        created.Status.Should().Be(RotationStatus.NotStarted);

        // Round-trips through a fresh read.
        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{created.Id}", JsonOptions);
        fetched!.StudentName.Should().Be("Dana Cole");
        fetched.StudentId.Should().Be(DanaColeStudentId);
    }

    [Fact]
    public async Task Create_snapshots_the_student_oid_when_the_directory_record_has_one()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh student carrying a CIAM oid; the rotation must snapshot it.
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Linked", "Learner", $"linked.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, "ciam-oid-xyz"),
                JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var created = await (await PostAsync(admin, ValidCreate(studentId: student!.Id)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        created!.StudentOid.Should().Be("ciam-oid-xyz");
    }

    [Fact]
    public async Task Create_with_unknown_program_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(programId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_unknown_student_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(studentId: Guid.NewGuid()));

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
    public async Task Admin_can_update_a_rotation_status_dates_and_student()
    {
        var admin = Client(RoleNames.Admin);
        // Start at NotStarted so NotStarted → Active is a legal transition.
        var created = await (await PostAsync(admin, ValidCreate(status: RotationStatus.NotStarted)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        // Re-point to a different student + a longer range + a legal status move.
        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, DanaColeStudentId,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 19), RotationStatus.Active);   // 42 days → 6 weeks
        var response = await admin.PutAsJsonAsync($"/api/rotations/{created!.Id}", update, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{created.Id}", JsonOptions);
        fetched!.Status.Should().Be(RotationStatus.Active);
        fetched.StudentId.Should().Be(DanaColeStudentId);
        fetched.StudentName.Should().Be("Dana Cole");   // re-snapshotted from the new student
        fetched.Weeks.Should().Be(6);                   // re-derived from the new range
    }

    [Fact]
    public async Task Update_with_an_illegal_status_transition_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        // Pending may only move to NotStarted/Rejected/Cancelled — not straight to Completed.
        var created = await (await PostAsync(admin, ValidCreate(status: RotationStatus.Pending)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, SamRiveraStudentId,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Completed);
        var response = await admin.PutAsJsonAsync($"/api/rotations/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The rotation is unchanged — still Pending.
        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{created.Id}", JsonOptions);
        fetched!.Status.Should().Be(RotationStatus.Pending);
    }

    [Fact]
    public async Task Detail_exposes_the_allowed_next_statuses()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(status: RotationStatus.Pending)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{created!.Id}", JsonOptions);

        fetched!.AllowedNextStatuses.Should().BeEquivalentTo(
            new[] { RotationStatus.NotStarted, RotationStatus.Rejected, RotationStatus.Cancelled });
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, SamRiveraStudentId,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending);
        var response = await admin.PutAsJsonAsync($"/api/rotations/{Guid.NewGuid()}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_unknown_student_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var update = new UpdateRotationRequest(
            InternalMedicineProgramId, Guid.NewGuid(),
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
            Guid.NewGuid(), SamRiveraStudentId,
            new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending);
        var response = await admin.PutAsJsonAsync($"/api/rotations/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task Admin_can_soft_delete_a_rotation_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(studentId: DanaColeStudentId)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/rotations/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/rotations/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Search by the created rotation's own number so the NotContain is meaningful (not a false pass from
        // the row simply being off page 1 of an unfiltered list).
        var list = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            $"/api/rotations?q={created.RotationNumber}&pageSize=100", JsonOptions);
        list!.Items.Select(r => r.Id).Should().NotContain(created.Id);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(status: RotationStatus.Rejected)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var rejected = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?status=Rejected&pageSize=100", JsonOptions);
        rejected!.Items.Select(r => r.Id).Should().Contain(created!.Id);
        rejected.Items.Should().OnlyContain(r => r.Status == RotationStatus.Rejected);
    }

    [Fact]
    public async Task List_filters_by_program()
    {
        var admin = Client(RoleNames.Admin);

        var byProgram = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            $"/api/rotations?programId={InternalMedicineProgramId}&pageSize=100", JsonOptions);

        byProgram!.Items.Should().Contain(r => r.Id == SeededRotationId);
        byProgram.Items.Should().OnlyContain(r => r.SpecialtyName == "Internal Medicine");
    }

    [Fact]
    public async Task Deleting_a_program_with_a_rotation_is_blocked_and_the_list_still_loads()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh program + a rotation booked against it, isolated from the seeded data.
        var program = await (await admin.PostAsJsonAsync("/api/programs",
                new CreateProgramRequest(InternalMedicineSpecialtyId, ProgramType.InPerson, 2, 4, 1500m, 500m, "Deletable?", null), JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        var rotation = await (await PostAsync(admin, ValidCreate(programId: program!.Id)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/programs/{program.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var list = await admin.GetAsync("/api/rotations?pageSize=100");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await list.Content.ReadFromJsonAsync<PagedResponse<RotationSummaryResponse>>(JsonOptions);
        rows!.Items.Should().Contain(r => r.Id == rotation!.Id);

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

    [Fact]
    public async Task Pages_partition_the_set_without_overlap_or_skips()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh program with exactly three rotations (distinct start dates) so the total is deterministic
        // regardless of the shared DB's other rows.
        var program = await (await admin.PostAsJsonAsync("/api/programs",
                new CreateProgramRequest(InternalMedicineSpecialtyId, ProgramType.InPerson, 2, 4, 1500m, 500m, "Paging set", null), JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        for (var i = 0; i < 3; i++)
        {
            (await PostAsync(admin, ValidCreate(programId: program!.Id, studentId: DanaColeStudentId,
                start: new DateOnly(2027, 3, 1 + i), end: new DateOnly(2027, 4, 1 + i))))
                .EnsureSuccessStatusCode();
        }

        var baseUrl = $"/api/rotations?programId={program!.Id}";
        var page1 = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>($"{baseUrl}&page=1&pageSize=2", JsonOptions);
        var page2 = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>($"{baseUrl}&page=2&pageSize=2", JsonOptions);
        var all = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>($"{baseUrl}&pageSize=100", JsonOptions);

        page1!.TotalCount.Should().Be(3);
        page2!.TotalCount.Should().Be(3);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(2);
        page1.TotalPages.Should().Be(2);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(1); // the remainder

        var page1Ids = page1.Items.Select(r => r.Id).ToList();
        var page2Ids = page2.Items.Select(r => r.Id).ToList();
        page1Ids.Should().NotIntersectWith(page2Ids);                       // no row on two pages
        page1Ids.Concat(page2Ids).Should().BeEquivalentTo(all!.Items.Select(r => r.Id)); // none skipped
        // Pages follow the same order as the full list (StartDate desc, RotationNumber desc).
        page1Ids.Concat(page2Ids).Should().ContainInOrder(all.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task List_normalizes_out_of_range_paging_parameters()
    {
        var admin = Client(RoleNames.Admin);

        // page=0 floors to 1; pageSize=0 falls back to the default — neither 500s.
        var page = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?page=0&pageSize=0", JsonOptions);

        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(10); // DefaultPageSize
    }

    [Fact]
    public async Task List_pageSize_is_capped_at_the_maximum()
    {
        var admin = Client(RoleNames.Admin);

        // Asking for an absurd page size is clamped to the server maximum (100), not honoured.
        var page = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?pageSize=100000", JsonOptions);

        page!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task List_search_matches_the_rotation_number_with_or_without_the_R_prefix()
    {
        var admin = Client(RoleNames.Admin);

        // The seeded rotation is number 1001. Both "R1001" and "1001" must find it; a non-matching term must not.
        var withR = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=R1001&pageSize=100", JsonOptions);
        withR!.Items.Should().Contain(r => r.Id == SeededRotationId);

        var withoutR = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=1001&pageSize=100", JsonOptions);
        withoutR!.Items.Should().Contain(r => r.Id == SeededRotationId);

        var miss = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=zzz-no-such-rotation&pageSize=100", JsonOptions);
        miss!.Items.Should().NotContain(r => r.Id == SeededRotationId);

        // "R" + the wrong number must NOT over-match the seeded row (guards the R-strip from matching all).
        var wrongNumber = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=R9999&pageSize=100", JsonOptions);
        wrongNumber!.Items.Should().NotContain(r => r.Id == SeededRotationId);
    }

    [Fact]
    public async Task List_search_matches_the_preceptor_full_name_across_the_space()
    {
        var admin = Client(RoleNames.Admin);

        // The seeded rotation's preceptor is "Jane Carter" — a match only works if the first+last concat is
        // searched server-side (this is the case that fails if the concatenated ILIKE doesn't translate).
        var byPreceptor = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=Jane Carter&pageSize=100", JsonOptions);

        byPreceptor!.Items.Should().Contain(r => r.Id == SeededRotationId);
    }

    [Fact]
    public async Task List_search_at_the_length_limit_is_accepted()
    {
        var admin = Client(RoleNames.Admin);

        // Exactly the max length is allowed (the reject is only ABOVE the limit) — guards the off-by-one.
        var response = await admin.GetAsync($"/api/rotations?q={new string('x', 100)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_search_matches_the_student_name()
    {
        var admin = Client(RoleNames.Admin);

        var bySam = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=Rivera&pageSize=100", JsonOptions);

        const StringComparison ci = StringComparison.OrdinalIgnoreCase;
        bySam!.Items.Should().Contain(r => r.Id == SeededRotationId);
        bySam.Items.Should().OnlyContain(r =>
            r.StudentName.Contains("Rivera", ci) || r.StudentEmail.Contains("Rivera", ci)
            || r.SpecialtyName.Contains("Rivera", ci) || (r.PreceptorName != null && r.PreceptorName.Contains("Rivera", ci)));
    }

    [Fact]
    public async Task List_search_over_the_length_limit_is_rejected()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.GetAsync($"/api/rotations?q={new string('x', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Production-parity columns (PR-3): Retail Amount, Needs Visa, Current/Historical scope ----

    [Fact]
    public async Task Admin_list_carries_the_retail_amount_and_needs_visa_flag()
    {
        var admin = Client(RoleNames.Admin);

        var list = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            "/api/rotations?q=R1001&pageSize=100", JsonOptions);
        var seeded = list!.Items.Single(r => r.Id == SeededRotationId);
        seeded.RetailAmount.Should().Be(6000m);  // seeded IM program retail 1500/wk × 4 weeks
        seeded.NeedsVisa.Should().BeTrue();        // Sam Rivera's visa status is NeedsVisaHelp

        // A Dana Cole booking (no visa help) → NeedsVisa false.
        var created = await (await PostAsync(admin, ValidCreate(studentId: DanaColeStudentId)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        var danaList = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            $"/api/rotations?q={created!.RotationNumber}&pageSize=100", JsonOptions);
        danaList!.Items.Single(r => r.Id == created.Id).NeedsVisa.Should().BeFalse();
    }

    [Fact]
    public async Task List_scope_splits_current_and_historical_by_status()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh program isolates the assertion from the shared DB's other rows (programId + scope filter).
        var program = await (await admin.PostAsJsonAsync("/api/programs",
                new CreateProgramRequest(InternalMedicineSpecialtyId, ProgramType.InPerson, 2, 4, 1500m, 500m, "Scope set", null), JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        var current = await (await PostAsync(admin, ValidCreate(programId: program!.Id, studentId: DanaColeStudentId,
                start: new DateOnly(2027, 5, 3), end: new DateOnly(2027, 5, 31), status: RotationStatus.NotStarted)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        var historical = await (await PostAsync(admin, ValidCreate(programId: program.Id, studentId: DanaColeStudentId,
                start: new DateOnly(2027, 6, 7), end: new DateOnly(2027, 7, 5), status: RotationStatus.Rejected)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var currentList = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            $"/api/rotations?programId={program.Id}&scope=current&pageSize=100", JsonOptions);
        var historicalList = await admin.GetFromJsonAsync<PagedResponse<RotationSummaryResponse>>(
            $"/api/rotations?programId={program.Id}&scope=historical&pageSize=100", JsonOptions);

        currentList!.Items.Select(r => r.Id).Should().Contain(current!.Id).And.NotContain(historical!.Id);
        historicalList!.Items.Select(r => r.Id).Should().Contain(historical.Id).And.NotContain(current.Id);

        var terminal = new[] { RotationStatus.Completed, RotationStatus.Cancelled, RotationStatus.Refunded, RotationStatus.Abandoned, RotationStatus.Rejected };
        currentList.Items.Should().OnlyContain(r => !terminal.Contains(r.Status));
        historicalList.Items.Should().OnlyContain(r => terminal.Contains(r.Status));
    }

    [Fact]
    public async Task Detail_carries_program_number_retail_cost_and_zero_paid_amount_for_a_new_booking()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(studentId: DanaColeStudentId)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        created!.ProgramNumber.Should().BeGreaterThan(0);
        created.RetailAmount.Should().Be(6000m);  // 1500/wk × 4 weeks
        created.PaidAmount.Should().Be(0m);         // brand-new booking — nothing captured yet
    }

    [Fact]
    public async Task Detail_paid_amount_reflects_a_captured_deposit()
    {
        var admin = Client(RoleNames.Admin);
        var oid = $"ciam-{Guid.NewGuid():N}";
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Paid", "Learner", $"paid.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        var rotation = await (await PostAsync(admin, ValidCreate(studentId: student!.Id)))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        // Drive the deposit to success via the DEV simulate endpoint (fake gateway) as the booking's student.
        var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        customer.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);
        var paymentId = (await (await customer.PostAsync($"/api/rotations/{rotation!.Id}/payment-intent", null))
            .Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        (await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{rotation.Id}", JsonOptions);
        fetched!.PaidAmount.Should().BeGreaterThan(0m); // the captured deposit shows as Paid Amount
    }
}
