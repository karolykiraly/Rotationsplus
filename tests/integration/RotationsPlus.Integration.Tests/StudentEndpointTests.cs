using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Common;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

public class StudentEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded students (see StudentConfiguration).
    private static readonly Guid SamRiveraId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    private const string SeededSamEmail = "sam.rivera@example.com";

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
    private static string UniqueEmail() => $"student.{Guid.NewGuid():N}@example.com";

    private static CreateStudentRequest ValidCreate(
        string firstName = "Test",
        string lastName = "Student",
        string? email = null,
        string? phone = "+1 555 0100",
        AcademicStatus academicStatus = AcademicStatus.MdStudent,
        VisaStatus? visaStatus = VisaStatus.CitizenOrGreenCard,
        string? medicalSchool = "State University",
        string? medicalSchoolCountry = "USA",
        string? city = "San Diego",
        string? state = "CA",
        StudentStatus status = StudentStatus.Registered,
        string? oid = null) =>
        new(firstName, lastName, email ?? UniqueEmail(), phone, academicStatus, visaStatus,
            medicalSchool, medicalSchoolCountry, city, state, status, oid);

    private Task<HttpResponseMessage> PostAsync(HttpClient client, CreateStudentRequest body) =>
        client.PostAsJsonAsync("/api/students", body, JsonOptions);

    // ---- Reads (StaffOnly) ----

    [Fact]
    public async Task Staff_list_includes_the_seeded_student()
    {
        var coordinator = Client(RoleNames.Coordinator);

        // Narrow to the seeded student by name so the assertion is deterministic in the shared, growing DB.
        var list = await coordinator.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            "/api/students?q=Rivera&pageSize=100", JsonOptions);

        var sam = list!.Items.SingleOrDefault(s => s.Id == SamRiveraId);
        sam.Should().NotBeNull();
        sam!.FullName.Should().Be("Sam Rivera");
        sam.AcademicStatus.Should().Be(AcademicStatus.InternationalMedicalGraduate);
        sam.VisaStatus.Should().Be(VisaStatus.NeedsVisaHelp);
        sam.Status.Should().Be(StudentStatus.MemberActivated);
    }

    [Fact]
    public async Task Staff_can_get_the_seeded_student_by_id()
    {
        var staff = Client(RoleNames.Sales);

        var student = await staff.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{SamRiveraId}", JsonOptions);

        student!.FirstName.Should().Be("Sam");
        student.City.Should().Be("Chicago");
        student.AcademicStatus.Should().Be(AcademicStatus.InternationalMedicalGraduate);
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var staff = Client(RoleNames.Sales);

        var response = await staff.GetAsync($"/api/students/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_cannot_list_students()
    {
        var student = Client(RoleNames.Student);

        var response = await student.GetAsync("/api/students");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_filters_by_academic_status()
    {
        var admin = Client(RoleNames.Admin);

        // A fresh PA student, isolated from the seeded records.
        var created = await (await PostAsync(admin, ValidCreate(academicStatus: AcademicStatus.PhysicianAssistantStudent)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var pas = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            "/api/students?academicStatus=PhysicianAssistantStudent&pageSize=100", JsonOptions);
        pas!.Items.Select(s => s.Id).Should().Contain(created!.Id);
        pas.Items.Should().OnlyContain(s => s.AcademicStatus == AcademicStatus.PhysicianAssistantStudent);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(status: StudentStatus.TurnedIntoContact)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var contacts = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            "/api/students?status=TurnedIntoContact&pageSize=100", JsonOptions);
        contacts!.Items.Select(s => s.Id).Should().Contain(created!.Id);
        contacts.Items.Should().OnlyContain(s => s.Status == StudentStatus.TurnedIntoContact);
    }

    [Fact]
    public async Task List_paginates_and_searches_by_name_and_email()
    {
        var admin = Client(RoleNames.Admin);

        // Three students sharing a unique surname token → a q on it returns exactly those three, deterministic.
        var token = $"Pagington{Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++)
        {
            (await PostAsync(admin, ValidCreate(firstName: $"P{i}", lastName: token))).EnsureSuccessStatusCode();
        }

        var page1 = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            $"/api/students?q={token}&page=1&pageSize=2", JsonOptions);
        var page2 = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            $"/api/students?q={token}&page=2&pageSize=2", JsonOptions);

        page1!.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page2!.Items.Should().HaveCount(1);
        page1.Items.Select(s => s.Id).Should().NotIntersectWith(page2.Items.Select(s => s.Id)); // no overlap
        page1.Items.Concat(page2.Items).Should().OnlyContain(s => s.FullName.Contains(token));
    }

    [Fact]
    public async Task List_search_matches_email()
    {
        var admin = Client(RoleNames.Admin);
        var email = UniqueEmail();
        var created = await (await PostAsync(admin, ValidCreate(email: email)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        // Search by a distinctive fragment of the email.
        var frag = email.Split('@')[0];
        var found = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            $"/api/students?q={frag}&pageSize=100", JsonOptions);

        found!.Items.Should().ContainSingle(s => s.Id == created!.Id);
    }

    [Fact]
    public async Task List_search_over_the_length_limit_is_rejected()
    {
        var admin = Client(RoleNames.Admin);

        var response = await admin.GetAsync($"/api/students?q={new string('x', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Options_returns_the_unpaginated_picker_list_including_the_seeded_student()
    {
        var staff = Client(RoleNames.Coordinator);

        // The options endpoint is for form pickers — it returns the full list, not a page.
        var options = await staff.GetFromJsonAsync<List<StudentSummaryResponse>>("/api/students/options", JsonOptions);

        options!.Should().Contain(s => s.Id == SamRiveraId);
    }

    [Fact]
    public async Task Customer_cannot_list_student_options()
    {
        var student = Client(RoleNames.Student);

        var response = await student.GetAsync("/api/students/options");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Admin writes ----

    [Fact]
    public async Task Admin_can_create_then_fetch_a_student()
    {
        var admin = Client(RoleNames.Admin);

        var create = await PostAsync(admin, ValidCreate(
            firstName: "Alice", lastName: "Nguyen", academicStatus: AcademicStatus.InternationalMedicalStudent,
            visaStatus: VisaStatus.ValidVisa, city: "Boston", state: "MA", oid: "ciam-oid-alice"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Alice");
        created.AcademicStatus.Should().Be(AcademicStatus.InternationalMedicalStudent);
        created.VisaStatus.Should().Be(VisaStatus.ValidVisa);
        created.City.Should().Be("Boston");
        created.StudentOid.Should().Be("ciam-oid-alice");

        var fetched = await admin.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{created.Id}", JsonOptions);
        fetched!.FirstName.Should().Be("Alice");
        fetched.AcademicStatus.Should().Be(AcademicStatus.InternationalMedicalStudent);
    }

    [Fact]
    public async Task Create_without_a_visa_status_leaves_it_null()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(visaStatus: null)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        created!.VisaStatus.Should().BeNull();
    }

    [Fact]
    public async Task Create_trims_name_whitespace()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(firstName: "  Margaret  ")))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        created!.FirstName.Should().Be("Margaret");
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

        var response = await PostAsync(admin, ValidCreate(email: SeededSamEmail));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_with_oversized_phone_returns_400()
    {
        var admin = Client(RoleNames.Admin);

        var response = await PostAsync(admin, ValidCreate(phone: new string('9', 41)));

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
    public async Task Create_with_an_oid_already_linked_to_a_live_student_returns_409()
    {
        var admin = Client(RoleNames.Admin);
        var oid = $"ciam-{Guid.NewGuid():N}";
        await PostAsync(admin, ValidCreate(oid: oid));

        // A second, distinct student (different email) can't take the same CIAM oid.
        var response = await PostAsync(admin, ValidCreate(oid: oid));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_to_an_oid_already_linked_to_a_live_student_returns_409()
    {
        var admin = Client(RoleNames.Admin);
        var oid = $"ciam-{Guid.NewGuid():N}";
        await PostAsync(admin, ValidCreate(oid: oid));
        var other = await (await PostAsync(admin, ValidCreate(oid: null)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var update = new UpdateStudentRequest(
            other!.FirstName, other.LastName, other.Email, null, AcademicStatus.MdStudent, null,
            null, null, null, null, StudentStatus.Registered, oid);   // tries to take the linked oid
        var response = await admin.PutAsJsonAsync($"/api/students/{other.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Recreating_a_soft_deleted_student_restores_and_refreshes_it()
    {
        var admin = Client(RoleNames.Admin);
        var email = UniqueEmail();
        var created = await (await PostAsync(admin, ValidCreate(firstName: "Original", email: email)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        await admin.DeleteAsync($"/api/students/{created!.Id}");

        var recreate = await PostAsync(admin, ValidCreate(firstName: "Refreshed", email: email));
        recreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var restored = await recreate.Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        restored!.Id.Should().Be(created.Id);        // same row, undeleted
        restored.FirstName.Should().Be("Refreshed"); // and refreshed with the new data
    }

    [Fact]
    public async Task Admin_can_update_a_student()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var update = new UpdateStudentRequest(
            "Updated", "Name", created!.Email, "+1 555 0199", AcademicStatus.DoStudent, VisaStatus.InterviewScheduled,
            "New School", "Canada", "Albany", "NY", StudentStatus.MemberActivated, "linked-oid");
        var response = await admin.PutAsJsonAsync($"/api/students/{created.Id}", update, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{created.Id}", JsonOptions);
        fetched!.FirstName.Should().Be("Updated");
        fetched.AcademicStatus.Should().Be(AcademicStatus.DoStudent);
        fetched.VisaStatus.Should().Be(VisaStatus.InterviewScheduled);
        fetched.MedicalSchoolCountry.Should().Be("Canada");
        fetched.Status.Should().Be(StudentStatus.MemberActivated);
        fetched.StudentOid.Should().Be("linked-oid");
    }

    [Fact]
    public async Task Update_can_clear_the_visa_status()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate(visaStatus: VisaStatus.ValidVisa)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var update = new UpdateStudentRequest(
            created!.FirstName, created.LastName, created.Email, null, AcademicStatus.MdStudent, null,
            null, null, null, null, StudentStatus.Registered, null);
        (await admin.PutAsJsonAsync($"/api/students/{created.Id}", update, JsonOptions)).StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{created.Id}", JsonOptions);
        fetched!.VisaStatus.Should().BeNull();
    }

    [Fact]
    public async Task Update_with_invalid_email_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var update = new UpdateStudentRequest("A", "B", "not-an-email", null, AcademicStatus.MdStudent, null,
            null, null, null, null, StudentStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/students/{created!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_to_an_existing_email_returns_409()
    {
        var admin = Client(RoleNames.Admin);
        var first = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        var second = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var update = new UpdateStudentRequest("X", "Y", first!.Email, null, AcademicStatus.MdStudent, null,
            null, null, null, null, StudentStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/students/{second!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_to_email_of_a_soft_deleted_student_returns_409()
    {
        var admin = Client(RoleNames.Admin);

        // A student we soft-delete — its email stays reserved by the (unfiltered) unique index.
        var deletedEmail = UniqueEmail();
        var toDelete = await (await PostAsync(admin, ValidCreate(email: deletedEmail)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        (await admin.DeleteAsync($"/api/students/{toDelete!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A live student that tries to take the soft-deleted one's email.
        var live = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        var update = new UpdateStudentRequest("A", "B", deletedEmail, null, AcademicStatus.MdStudent, null,
            null, null, null, null, StudentStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/students/{live!.Id}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);

        var update = new UpdateStudentRequest("A", "B", UniqueEmail(), null, AcademicStatus.MdStudent, null,
            null, null, null, null, StudentStatus.Registered, null);
        var response = await admin.PutAsJsonAsync($"/api/students/{Guid.NewGuid()}", update, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_a_student_with_a_booked_rotation_is_blocked()
    {
        var admin = Client(RoleNames.Admin);

        // The seeded Sam Rivera student is booked into the seeded rotation, so deletion is blocked.
        var response = await admin.DeleteAsync($"/api/students/{SamRiveraId}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // And the record is still there.
        (await admin.GetAsync($"/api/students/{SamRiveraId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_student_whose_only_rotation_is_soft_deleted_becomes_deletable()
    {
        var admin = Client(RoleNames.Admin);
        var internalMedicineProgram = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

        var student = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(internalMedicineProgram, student!.Id,
                    new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        // While the rotation is live, the student can't be deleted.
        (await admin.DeleteAsync($"/api/students/{student.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Soft-delete the rotation → the student becomes deletable (soft-deleted rotations don't count).
        (await admin.DeleteAsync($"/api/rotations/{rotation!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/students/{student.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Admin_can_soft_delete_a_student_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/students/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/students/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>("/api/students?pageSize=100", JsonOptions);
        list!.Items.Select(s => s.Id).Should().NotContain(created.Id);
    }

    // ---- Profile → Personal Information tab ----

    [Fact]
    public async Task Admin_saves_the_personal_information_tab_and_it_round_trips()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var req = new UpdateStudentPersonalInfoRequest(
            "Greeshma", "James", "+1 555 0142", AcademicStatus.InternationalMedicalGraduate,
            new DateOnly(1996, 4, 22), Gender.Female, ImmigrationStatus.B1B2, null, null,
            "India", "P1234567", null, null);
        var resp = await admin.PutAsJsonAsync($"/api/students/{created!.Id}/personal-info", req, JsonOptions);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await admin.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{created.Id}", JsonOptions);
        fetched!.FirstName.Should().Be("Greeshma");
        fetched.LastName.Should().Be("James");
        fetched.Birthdate.Should().Be(new DateOnly(1996, 4, 22));
        fetched.Gender.Should().Be(Gender.Female);
        fetched.ImmigrationStatus.Should().Be(ImmigrationStatus.B1B2);
        fetched.PassportIssuedCountry.Should().Be("India");
        fetched.PassportNumber.Should().Be("P1234567");
    }

    [Fact]
    public async Task Personal_info_save_with_a_blank_first_name_returns_400()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var req = new UpdateStudentPersonalInfoRequest(
            "   ", "James", null, AcademicStatus.MdStudent, null, null, null, null, null, null, null, null, null);
        var resp = await admin.PutAsJsonAsync($"/api/students/{created!.Id}/personal-info", req, JsonOptions);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Personal_info_save_for_an_unknown_id_returns_404()
    {
        var admin = Client(RoleNames.Admin);
        var req = new UpdateStudentPersonalInfoRequest(
            "A", "B", null, AcademicStatus.MdStudent, null, null, null, null, null, null, null, null, null);
        var resp = await admin.PutAsJsonAsync($"/api/students/{Guid.NewGuid()}/personal-info", req, JsonOptions);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Achievements rollups (money-sensitive: the Contacts → Students tab columns) ----

    // Non-open program: 1500/wk. A 4-week booking → total 6000, deposit 600, outstanding 5400 (matches
    // PaymentCheckoutEndpointTests). The rollups are all keyed off SUCCEEDED payments, so an unpaid
    // second booking must NOT inflate DollarsSpent / OutstandingPayments / WeeksPurchased.
    private static readonly Guid InternalMedicineProgram = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public async Task Student_summary_rolls_up_only_paid_bookings_into_the_achievements_columns()
    {
        var admin = Client(RoleNames.Admin);
        var oid = $"ciam-{Guid.NewGuid():N}";
        var token = $"Rollup{Guid.NewGuid():N}";

        // A student with a distinctive surname token so the list query returns exactly this row.
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Rolla", token, UniqueEmail(), null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        // Paid booking: 4 weeks, deposit driven to Succeeded via the DEV simulate round-trip.
        var paidRotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(InternalMedicineProgram, student!.Id,
                    new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), RotationStatus.Pending), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);

        var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        customer.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);
        var paymentId = (await (await customer.PostAsync($"/api/rotations/{paidRotation!.Id}/payment-intent", null))
            .Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        (await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions))
            .EnsureSuccessStatusCode();

        // A SECOND, unpaid booking (still Pending) — must not touch the money/weeks rollups.
        (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(InternalMedicineProgram, student.Id,
                    new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 30), RotationStatus.Pending), JsonOptions))
            .EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>(
            $"/api/students?q={token}&pageSize=100", JsonOptions);
        var summary = list!.Items.Single(s => s.Id == student.Id);

        // Only the paid rotation counts.
        summary.DollarsSpent.Should().Be(600m);          // deposit collected
        summary.OutstandingPayments.Should().Be(5400m);  // remainder billed later
        summary.WeeksPurchased.Should().Be(4);           // weeks on the paid booking only

        // Outstanding documents = every not-yet-uploaded required doc across BOTH bookings; cross-check
        // the projection against a direct DB count so the "needs action" state set stays honest.
        int expectedOutstandingDocs;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
            expectedOutstandingDocs = await db.RotationDocuments.CountAsync(rd =>
                rd.StudentId == student.Id
                && (rd.Status == DocumentStatus.UploadNeeded
                    || rd.Status == DocumentStatus.Rejected
                    || rd.Status == DocumentStatus.Expired));
        }
        summary.OutstandingDocuments.Should().Be(expectedOutstandingDocs);
    }
}
