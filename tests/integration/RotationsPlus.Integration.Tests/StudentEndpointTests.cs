using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
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

        var list = await coordinator.GetFromJsonAsync<List<StudentSummaryResponse>>("/api/students", JsonOptions);

        var sam = list!.SingleOrDefault(s => s.Id == SamRiveraId);
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

        var pas = await admin.GetFromJsonAsync<List<StudentSummaryResponse>>(
            "/api/students?academicStatus=PhysicianAssistantStudent", JsonOptions);
        pas!.Select(s => s.Id).Should().Contain(created!.Id);
        pas.Should().OnlyContain(s => s.AcademicStatus == AcademicStatus.PhysicianAssistantStudent);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var admin = Client(RoleNames.Admin);

        var created = await (await PostAsync(admin, ValidCreate(status: StudentStatus.TurnedIntoContact)))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var contacts = await admin.GetFromJsonAsync<List<StudentSummaryResponse>>(
            "/api/students?status=TurnedIntoContact", JsonOptions);
        contacts!.Select(s => s.Id).Should().Contain(created!.Id);
        contacts.Should().OnlyContain(s => s.Status == StudentStatus.TurnedIntoContact);
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
    public async Task Admin_can_soft_delete_a_student_and_it_disappears_from_the_list()
    {
        var admin = Client(RoleNames.Admin);
        var created = await (await PostAsync(admin, ValidCreate()))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);

        var delete = await admin.DeleteAsync($"/api/students/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await admin.GetAsync($"/api/students/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await admin.GetFromJsonAsync<List<StudentSummaryResponse>>("/api/students", JsonOptions);
        list!.Select(s => s.Id).Should().NotContain(created.Id);
    }
}
