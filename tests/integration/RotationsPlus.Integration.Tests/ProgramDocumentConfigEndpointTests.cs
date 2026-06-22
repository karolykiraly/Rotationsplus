using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// PHASE 2g-3a: admin required-docs configuration — set a program's required document types + due-days,
/// add a custom document type, and confirm a new booking materializes the configured set with the
/// configured due-date offset.
/// </summary>
public class ProgramDocumentConfigEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineSpecialtyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-admin");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Admin);
        return client;
    }

    private HttpClient Customer(string oid)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);
        return client;
    }

    private static string UniqueOid() => $"ciam-{Guid.NewGuid():N}";

    private async Task<Guid> CreateProgramAsync(HttpClient admin)
    {
        var detail = await (await admin.PostAsJsonAsync("/api/programs",
                new CreateProgramRequest(InternalMedicineSpecialtyId, ProgramType.InPerson, 2, 4, 500m, 100m,
                    "Config test program", PreceptorId: null), JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        return detail!.Id;
    }

    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Cfg", "Student", $"cfg.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    [Fact]
    public async Task Setting_required_docs_drives_materialization_with_the_configured_due_days()
    {
        var admin = Admin();
        var programId = await CreateProgramAsync(admin);

        // A brand-new program starts with no required docs and the default 14 due-days.
        var initial = await admin.GetFromJsonAsync<ProgramRequiredDocumentsResponse>(
            $"/api/programs/{programId}/required-documents", JsonOptions);
        initial!.RequiredDocumentTypeIds.Should().BeEmpty();
        initial.DocumentDueDays.Should().Be(14);
        initial.Catalog.Should().NotBeEmpty();

        // Require the first two catalog types, due 7 days before start.
        var twoTypeIds = initial.Catalog.Take(2).Select(t => t.Id).ToList();
        var put = await admin.PutAsJsonAsync($"/api/programs/{programId}/required-documents",
            new SetProgramRequiredDocumentsRequest(7, twoTypeIds), JsonOptions);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await admin.GetFromJsonAsync<ProgramRequiredDocumentsResponse>(
            $"/api/programs/{programId}/required-documents", JsonOptions);
        after!.DocumentDueDays.Should().Be(7);
        after.RequiredDocumentTypeIds.Should().BeEquivalentTo(twoTypeIds);

        // A booking on the program now materializes exactly those two docs, due 7 days before start.
        var oid = UniqueOid();
        await CreateStudentAsync(admin, oid);
        var booked = await (await Customer(oid).PostAsJsonAsync("/api/customer/rotations",
                new CustomerBookingRequest(programId, new DateOnly(2026, 12, 1), 4), JsonOptions))
            .Content.ReadFromJsonAsync<CustomerRotationResponse>(JsonOptions);

        var docs = await Customer(oid).GetFromJsonAsync<List<RotationDocumentResponse>>(
            $"/api/customer/rotations/{booked!.Id}/documents", JsonOptions);
        docs!.Should().HaveCount(2);
        docs.Should().OnlyContain(d => d.DueDate == new DateOnly(2026, 11, 24)); // 2026-12-01 − 7 days
    }

    [Fact]
    public async Task Adding_a_custom_document_type_appears_in_the_catalog_and_rejects_duplicates()
    {
        var admin = Admin();
        var name = $"Custom Doc {Guid.NewGuid():N}";

        var created = await admin.PostAsJsonAsync("/api/document-types",
            new CreateDocumentTypeRequest(name, DocumentCategory.Other), JsonOptions);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var catalog = await admin.GetFromJsonAsync<List<DocumentTypeResponse>>("/api/document-types", JsonOptions);
        catalog!.Should().Contain(t => t.Name == name && t.Category == DocumentCategory.Other);

        // Same name again → 409 (case-insensitive).
        var dup = await admin.PostAsJsonAsync("/api/document-types",
            new CreateDocumentTypeRequest(name.ToUpperInvariant(), DocumentCategory.Other), JsonOptions);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Removing_then_re_adding_a_required_type_works_across_the_soft_delete()
    {
        var admin = Admin();
        var programId = await CreateProgramAsync(admin);
        var catalog = (await admin.GetFromJsonAsync<ProgramRequiredDocumentsResponse>(
            $"/api/programs/{programId}/required-documents", JsonOptions))!.Catalog;
        var typeId = catalog.First().Id;

        // Add the type, remove it (soft-deletes the row), then add it back — the partial unique index
        // (active rows only) must permit the re-add rather than 500 on a uniqueness clash.
        await admin.PutAsJsonAsync($"/api/programs/{programId}/required-documents",
            new SetProgramRequiredDocumentsRequest(14, [typeId]), JsonOptions);
        await admin.PutAsJsonAsync($"/api/programs/{programId}/required-documents",
            new SetProgramRequiredDocumentsRequest(14, []), JsonOptions);
        var readd = await admin.PutAsJsonAsync($"/api/programs/{programId}/required-documents",
            new SetProgramRequiredDocumentsRequest(14, [typeId]), JsonOptions);

        readd.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await admin.GetFromJsonAsync<ProgramRequiredDocumentsResponse>(
            $"/api/programs/{programId}/required-documents", JsonOptions);
        after!.RequiredDocumentTypeIds.Should().ContainSingle().Which.Should().Be(typeId);
    }

    [Fact]
    public async Task Invalid_config_is_rejected()
    {
        var admin = Admin();
        var programId = await CreateProgramAsync(admin);

        var badDueDays = await admin.PutAsJsonAsync($"/api/programs/{programId}/required-documents",
            new SetProgramRequiredDocumentsRequest(999, []), JsonOptions);
        badDueDays.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badType = await admin.PutAsJsonAsync($"/api/programs/{programId}/required-documents",
            new SetProgramRequiredDocumentsRequest(14, [Guid.NewGuid()]), JsonOptions);
        badType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
