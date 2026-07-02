using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Api.Modules.Students;

/// <summary>
/// Student directory endpoints. Reads are StaffOnly (the whole staff console — sales/SDR/coordinator —
/// works the directory for CRM); writes are AdminOnly. This first slice is the identity + academic/visa
/// classification core; the deep profile, documents, payments, and the rotation link arrive in later
/// slices (see Plan_Student.md). A student record is matched to a CIAM account by <c>StudentOid</c>.
/// </summary>
public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/students")
            .RequireAuthorization(AuthorizationPolicies.StaffOnly)
            .WithTags("Students");

        group.MapGet("/", async (
            StudentStatus? status, AcademicStatus? academicStatus, string? q, int? page, int? pageSize,
            RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!PaginationExtensions.TryBuildSearchPattern(q, out var pattern, out var searchError))
            {
                return Results.BadRequest(searchError);
            }

            var query = db.Students.AsQueryable();
            if (status is { } s) query = query.Where(x => x.Status == s);
            if (academicStatus is { } a) query = query.Where(x => x.AcademicStatus == a);
            if (pattern is not null)
            {
                // Mirrors the old client-side search: name, email, and location (city/state). ILIKE = ci contains.
                query = query.Where(x =>
                    EF.Functions.ILike(x.FirstName + " " + x.LastName, pattern) ||
                    EF.Functions.ILike(x.Email, pattern) ||
                    (x.City != null && EF.Functions.ILike(x.City, pattern)) ||
                    (x.State != null && EF.Functions.ILike(x.State, pattern)));
            }

            var students = await query
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.Id) // tie-break so paging is deterministic when names collide
                // Inline projection (NOT ToSummary): the rollups are correlated subqueries that must
                // translate to SQL — a compiled helper would client-evaluate them (one query per row).
                .Select(x => new StudentSummaryResponse(
                    x.Id,
                    x.FirstName + " " + x.LastName,
                    x.Email,
                    x.MobilePhone,
                    x.AcademicStatus,
                    x.VisaStatus,
                    x.City,
                    x.State,
                    x.Status,
                    // Dollars Spent: money actually collected — the sum of SUCCEEDED payment amounts on
                    // this student's rotations (mirrors RotationEndpoints' PaidAmount).
                    db.Payments
                        .Where(p => p.Rotation.StudentId == x.Id && p.Status == PaymentStatus.Succeeded)
                        .Sum(p => (decimal?)p.Amount) ?? 0m,
                    // Outstanding Payments: the remainder still billed-later on those paid bookings — the
                    // payment's own recorded OutstandingAmount, summed over SUCCEEDED payments (refunded/
                    // failed/pending excluded, so nothing that isn't a live collected booking counts).
                    db.Payments
                        .Where(p => p.Rotation.StudentId == x.Id && p.Status == PaymentStatus.Succeeded)
                        .Sum(p => (decimal?)p.OutstandingAmount) ?? 0m,
                    // Outstanding Documents: required documents still needing a (re)upload across this
                    // student's rotations (UploadNeeded/Rejected/Expired = the "Missing" states). Excludes
                    // docs on a soft-deleted rotation, matching the dashboards' `!Rotation.IsDeleted` rule.
                    db.RotationDocuments
                        .Count(rd => rd.StudentId == x.Id
                            && !rd.Rotation.IsDeleted
                            && (rd.Status == DocumentStatus.UploadNeeded
                                || rd.Status == DocumentStatus.Rejected
                                || rd.Status == DocumentStatus.Expired)),
                    // Weeks Purchased: weeks on bookings the student has actually paid toward (a rotation
                    // with at least one SUCCEEDED payment) — keeps the two "purchase" columns consistent.
                    db.Rotations
                        .Where(r => r.StudentId == x.Id
                            && db.Payments.Any(p => p.RotationId == r.Id && p.Status == PaymentStatus.Succeeded))
                        .Sum(r => (int?)r.Weeks) ?? 0))
                .ToPagedResponseAsync(page, pageSize, cancellationToken);

            return Results.Ok(students);
        })
        .WithName("ListStudents");

        // Unpaginated lightweight list for form pickers (e.g. the rotation form's student dropdown), which
        // need every option, not a page. Same DTO + StaffOnly as the paginated list (no new data/audience),
        // ordered by name. Deliberately unbounded: fine at directory scale; if the student directory ever
        // grows past a comfortable dropdown, switch the picker to a server-side typeahead reusing the list's
        // `q` search and retire this. (Plan_Student.md.)
        group.MapGet("/options", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var options = await db.Students
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .Select(x => ToSummary(x))
                .ToListAsync(cancellationToken);

            return Results.Ok(options);
        })
        .WithName("ListStudentOptions");

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var student = await db.Students
                .Where(x => x.Id == id)
                .Select(x => ToDetail(x))
                .FirstOrDefaultAsync(cancellationToken);

            return student is null ? Results.NotFound() : Results.Ok(student);
        })
        .WithName("GetStudent");

        // ---- Admin writes (AdminOnly stacks on the group's StaffOnly) ----

        group.MapPost("/", async (CreateStudentRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalize(request.FirstName, request.LastName, request.Email, request.MobilePhone,
                    request.AcademicStatus, request.VisaStatus, request.MedicalSchool, request.MedicalSchoolCountry,
                    request.City, request.State, request.Status, request.StudentOid, out var norm, out var error))
            {
                return Results.BadRequest(error);
            }

            // Match past the soft-delete filter so the unique email index can't be violated: an active
            // email conflicts; a soft-deleted one is restored (and refreshed) instead of duplicated.
            var existing = await db.Students
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Email == norm.Email, cancellationToken);

            if (existing is { IsDeleted: false })
            {
                return Results.Conflict($"A student with email '{norm.Email}' already exists.");
            }

            // A CIAM oid links to exactly one live student (the portal matches the caller to their
            // student by oid). Exclude the row we're about to restore, if any.
            if (await OidTakenAsync(db, norm.StudentOid, existing?.Id ?? Guid.Empty, cancellationToken))
            {
                return Results.Conflict($"A student with CIAM object id '{norm.StudentOid}' is already linked.");
            }

            if (existing is { IsDeleted: true })
            {
                existing.IsDeleted = false;
                existing.DeletedAtUtc = null;
                existing.DeletedBy = null;
                Apply(existing, norm);
                await db.SaveChangesAsync(cancellationToken);
                return Results.Created($"/api/students/{existing.Id}", ToDetail(existing));
            }

            var student = new Student
            {
                FirstName = norm.FirstName,
                LastName = norm.LastName,
                Email = norm.Email,
            };
            Apply(student, norm);
            db.Students.Add(student);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" } pg)
            {
                // Lost a concurrent create race; report which unique index (email or oid) rejected it.
                return Results.Conflict(pg.ConstraintName?.Contains("StudentOid", StringComparison.Ordinal) == true
                    ? $"A student with CIAM object id '{norm.StudentOid}' is already linked."
                    : $"A student with email '{norm.Email}' already exists.");
            }

            return Results.Created($"/api/students/{student.Id}", ToDetail(student));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateStudent");

        group.MapPut("/{id:guid}", async (Guid id, UpdateStudentRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalize(request.FirstName, request.LastName, request.Email, request.MobilePhone,
                    request.AcademicStatus, request.VisaStatus, request.MedicalSchool, request.MedicalSchoolCountry,
                    request.City, request.State, request.Status, request.StudentOid, out var norm, out var error))
            {
                return Results.BadRequest(error);
            }

            var student = await db.Students.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (student is null)
            {
                return Results.NotFound();
            }

            // Email must be unique across every other student (active or soft-deleted — the unique index
            // spans both), excluding this row.
            var emailTaken = await db.Students
                .IgnoreQueryFilters()
                .AnyAsync(x => x.Email == norm.Email && x.Id != id, cancellationToken);
            if (emailTaken)
            {
                return Results.Conflict($"A student with email '{norm.Email}' already exists.");
            }

            // A CIAM oid links to exactly one live student (excluding this row).
            if (await OidTakenAsync(db, norm.StudentOid, id, cancellationToken))
            {
                return Results.Conflict($"A student with CIAM object id '{norm.StudentOid}' is already linked.");
            }

            Apply(student, norm);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDetail(student));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateStudent");

        // Profile → Personal Information tab save (legacy onSaveProfile1). Updates the identity core + the
        // Personal-Info fields; Email/Status/StudentOid are managed elsewhere (identity-linked), so this
        // tab leaves them untouched.
        group.MapPut("/{id:guid}/personal-info", async (
            Guid id, UpdateStudentPersonalInfoRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalizePersonalInfo(request, out var norm, out var error))
            {
                return Results.BadRequest(error);
            }

            var student = await db.Students.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (student is null)
            {
                return Results.NotFound();
            }

            student.FirstName = norm.FirstName;
            student.LastName = norm.LastName;
            student.MobilePhone = norm.MobilePhone;
            student.AcademicStatus = norm.AcademicStatus;
            student.Birthdate = norm.Birthdate;
            student.Gender = norm.Gender;
            student.ImmigrationStatus = norm.ImmigrationStatus;
            student.ImmigrationStatusOther = norm.ImmigrationStatusOther;
            student.VisaInterviewDate = norm.VisaInterviewDate;
            student.PassportIssuedCountry = norm.PassportIssuedCountry;
            student.PassportNumber = norm.PassportNumber;
            student.SelectedIdType = norm.SelectedIdType;
            student.IdNumber = norm.IdNumber;

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDetail(student));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateStudentPersonalInfo");

        // Profile → Needs tab save (legacy onSaveProfile2). Interests / preferred specialty / preferred
        // locations (+ free-text "Other") / priorities. Selections are stored as titles.
        group.MapPut("/{id:guid}/needs", async (
            Guid id, UpdateStudentNeedsRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var interests = CleanTitleList(request.Interests);
            var locations = CleanTitleList(request.SpecialtyLocations);
            var importants = CleanTitleList(request.Importants);
            var preferred = TrimmedOrNull(request.PreferredSpecialty);
            var custom = TrimmedOrNull(request.CustomSpecialtyLocation);

            if (preferred is { Length: > 200 }) return Results.BadRequest("PreferredSpecialty must be 200 characters or fewer.");
            if (custom is { Length: > 120 }) return Results.BadRequest("CustomSpecialtyLocation must be 120 characters or fewer.");
            // Legacy rule: selecting the "Other" location requires the free-text value.
            if (locations is not null
                && locations.Contains("Other", StringComparer.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(custom))
            {
                return Results.BadRequest("Enter the specialty location for 'Other'.");
            }

            var student = await db.Students.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (student is null)
            {
                return Results.NotFound();
            }

            student.Interests = interests;
            student.PreferredSpecialty = preferred;
            student.SpecialtyLocations = locations;
            // Only keep the free-text when "Other" is actually selected.
            student.CustomSpecialtyLocation =
                locations is not null && locations.Contains("Other", StringComparer.OrdinalIgnoreCase) ? custom : null;
            student.Importants = importants;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDetail(student));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateStudentNeeds");

        // Profile → Education tab save (legacy onSaveProfile3). Branches by academic track (IMS/IMG USMLE,
        // D.O. COMLEX, Pre-med, Dental); the client sends the active branch's fields and leaves the rest
        // null. We apply all fields — a student has a single academic track, so unused branches stay null.
        group.MapPut("/{id:guid}/education", async (
            Guid id, UpdateStudentEducationRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeEducation(request, out var norm, out var error))
            {
                return Results.BadRequest(error);
            }

            var student = await db.Students.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (student is null)
            {
                return Results.NotFound();
            }

            // School + country are shared by the IMS/IMG + Dental branches (same columns as the identity core).
            student.MedicalSchool = norm.MedicalSchool;
            student.MedicalSchoolCountry = norm.MedicalSchoolCountry;
            student.GraduationDate = norm.GraduationDate;

            student.UsmleStep1 = norm.UsmleStep1;
            student.UsmleScore1 = norm.UsmleScore1;
            student.UsmleAttempts1 = norm.UsmleAttempts1;
            student.UsmleDate1 = norm.UsmleDate1;
            student.UsmleStep2 = norm.UsmleStep2;
            student.UsmleScore2 = norm.UsmleScore2;
            student.UsmleAttempts2 = norm.UsmleAttempts2;
            student.UsmleDate2 = norm.UsmleDate2;
            student.UsmleStep3 = norm.UsmleStep3;
            student.UsmleScore3 = norm.UsmleScore3;
            student.UsmleAttempts3 = norm.UsmleAttempts3;
            student.UsmleDate3 = norm.UsmleDate3;
            student.EcfmgCertified = norm.EcfmgCertified;
            student.AppliedMatch = norm.AppliedMatch;

            student.ComlexLevel1Taken = norm.ComlexLevel1Taken;
            student.ComlexLevel1Passed = norm.ComlexLevel1Passed;
            student.ComlexLevel2 = norm.ComlexLevel2;
            student.ComlexLevel2Score = norm.ComlexLevel2Score;
            student.ComlexLevel2Attempts = norm.ComlexLevel2Attempts;
            student.ComlexLevel2Date = norm.ComlexLevel2Date;
            student.ComlexLevel3 = norm.ComlexLevel3;
            student.ComlexLevel3Score = norm.ComlexLevel3Score;
            student.ComlexLevel3Attempts = norm.ComlexLevel3Attempts;
            student.ComlexLevel3Date = norm.ComlexLevel3Date;

            student.Undergrad = norm.Undergrad;
            student.EducationYear = norm.EducationYear;
            student.IsAmsa = norm.IsAmsa;
            student.Association = norm.Association;
            student.IsLeadership = norm.IsLeadership;

            student.IsToefl = norm.IsToefl;
            student.IsIndbe = norm.IsIndbe;

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDetail(student));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateStudentEducation");

        group.MapDelete("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var student = await db.Students.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (student is null)
            {
                return Results.NotFound();
            }

            // Block deletion while live rotations are booked against this student (the rotation FK is
            // Restrict; this guards the soft-delete path too). Soft-deleted rotations don't count.
            if (await db.Rotations.AnyAsync(r => r.StudentId == id, cancellationToken))
            {
                return Results.Conflict("This student has rotations booked and can't be deleted.");
            }

            db.Students.Remove(student); // interceptor converts the delete into a soft-delete
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeleteStudent");

        return routes;
    }

    /// <summary>True if another LIVE student (excluding <paramref name="selfId"/>) already carries this
    /// CIAM oid. Uses the global soft-delete filter, matching the live-only unique index on the column.</summary>
    private static Task<bool> OidTakenAsync(RotationsDbContext db, string? oid, Guid selfId, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(oid)
            ? Task.FromResult(false)
            : db.Students.AnyAsync(s => s.StudentOid == oid && s.Id != selfId, cancellationToken);

    private const int NameMaxLength = 100;
    private const int EmailMaxLength = 256;
    private const int PhoneMaxLength = 40;
    private const int SchoolMaxLength = 200;
    private const int CountryMaxLength = 100;
    private const int CityMaxLength = 100;
    private const int StateMaxLength = 50;
    private const int OidMaxLength = 64;

    /// <summary>Validated, trimmed field values ready to persist.</summary>
    private sealed record NormalizedStudent(
        string FirstName, string LastName, string Email, string? MobilePhone,
        AcademicStatus AcademicStatus, VisaStatus? VisaStatus, string? MedicalSchool, string? MedicalSchoolCountry,
        string? City, string? State, StudentStatus Status, string? StudentOid);

    private static bool TryNormalize(
        string? firstName, string? lastName, string? email, string? mobilePhone,
        AcademicStatus academicStatus, VisaStatus? visaStatus, string? medicalSchool, string? medicalSchoolCountry,
        string? city, string? state, StudentStatus status, string? studentOid,
        out NormalizedStudent norm, out string error)
    {
        norm = null!;
        error = string.Empty;

        if (!TryRequired(firstName, NameMaxLength, "FirstName", out var first, out error)) return false;
        if (!TryRequired(lastName, NameMaxLength, "LastName", out var last, out error)) return false;
        if (!TryRequired(email, EmailMaxLength, "Email", out var mail, out error)) return false;
        if (!MailAddress.TryCreate(mail, out _))
        {
            error = "Email is not a valid address.";
            return false;
        }

        if (!Enum.IsDefined(academicStatus))
        {
            error = "AcademicStatus is invalid.";
            return false;
        }

        if (visaStatus is { } v && !Enum.IsDefined(v))
        {
            error = "VisaStatus is invalid.";
            return false;
        }

        if (!Enum.IsDefined(status))
        {
            error = "Status is invalid.";
            return false;
        }

        if (!TryOptional(mobilePhone, PhoneMaxLength, "MobilePhone", out var phone, out error)) return false;
        if (!TryOptional(medicalSchool, SchoolMaxLength, "MedicalSchool", out var school, out error)) return false;
        if (!TryOptional(medicalSchoolCountry, CountryMaxLength, "MedicalSchoolCountry", out var country, out error)) return false;
        if (!TryOptional(city, CityMaxLength, "City", out var normalizedCity, out error)) return false;
        if (!TryOptional(state, StateMaxLength, "State", out var normalizedState, out error)) return false;
        if (!TryOptional(studentOid, OidMaxLength, "StudentOid", out var oid, out error)) return false;

        norm = new NormalizedStudent(first, last, mail, phone, academicStatus, visaStatus, school, country,
            normalizedCity, normalizedState, status, oid);
        return true;
    }

    private static bool TryRequired(string? input, int max, string field, out string value, out string error)
    {
        error = string.Empty;
        value = input?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            error = $"{field} is required.";
            return false;
        }

        if (value.Length > max)
        {
            error = $"{field} must be {max} characters or fewer.";
            return false;
        }

        return true;
    }

    private static bool TryOptional(string? input, int max, string field, out string? value, out string error)
    {
        error = string.Empty;
        value = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
        if (value is { } v && v.Length > max)
        {
            error = $"{field} must be {max} characters or fewer.";
            return false;
        }

        return true;
    }

    private const int ImmigrationOtherMaxLength = 120;
    private const int PassportNumberMaxLength = 60;
    private const int IdNumberMaxLength = 60;

    /// <summary>Validated Personal-Information tab values ready to persist.</summary>
    private sealed record NormalizedPersonalInfo(
        string FirstName, string LastName, string? MobilePhone, AcademicStatus AcademicStatus,
        DateOnly? Birthdate, Gender? Gender, ImmigrationStatus? ImmigrationStatus, string? ImmigrationStatusOther,
        DateOnly? VisaInterviewDate, string? PassportIssuedCountry, string? PassportNumber,
        StudentIdType? SelectedIdType, string? IdNumber);

    private static bool TryNormalizePersonalInfo(
        UpdateStudentPersonalInfoRequest r, out NormalizedPersonalInfo norm, out string error)
    {
        norm = null!;
        error = string.Empty;

        if (!TryRequired(r.FirstName, NameMaxLength, "FirstName", out var first, out error)) return false;
        if (!TryRequired(r.LastName, NameMaxLength, "LastName", out var last, out error)) return false;

        if (!Enum.IsDefined(r.AcademicStatus)) { error = "AcademicStatus is invalid."; return false; }
        if (r.Gender is { } g && !Enum.IsDefined(g)) { error = "Gender is invalid."; return false; }
        if (r.ImmigrationStatus is { } im && !Enum.IsDefined(im)) { error = "ImmigrationStatus is invalid."; return false; }
        if (r.SelectedIdType is { } sid && !Enum.IsDefined(sid)) { error = "SelectedIdType is invalid."; return false; }

        if (!TryOptional(r.MobilePhone, PhoneMaxLength, "MobilePhone", out var phone, out error)) return false;
        if (!TryOptional(r.ImmigrationStatusOther, ImmigrationOtherMaxLength, "ImmigrationStatusOther", out var imOther, out error)) return false;
        if (!TryOptional(r.PassportIssuedCountry, CountryMaxLength, "PassportIssuedCountry", out var passportCountry, out error)) return false;
        if (!TryOptional(r.PassportNumber, PassportNumberMaxLength, "PassportNumber", out var passportNo, out error)) return false;
        if (!TryOptional(r.IdNumber, IdNumberMaxLength, "IdNumber", out var idNo, out error)) return false;

        // Enforce the tab's conditional invariants server-side (don't trust the client): the D.O. track
        // carries an ID instead of a passport; the free-text override and interview date apply only to
        // their triggering immigration status. Mirrors the client's null-out so a direct API caller can't
        // persist an inconsistent combination.
        var isDo = r.AcademicStatus == AcademicStatus.DoStudent;
        norm = new NormalizedPersonalInfo(
            first, last, phone, r.AcademicStatus, r.Birthdate, r.Gender,
            r.ImmigrationStatus,
            r.ImmigrationStatus == ImmigrationStatus.Other ? imOther : null,
            r.ImmigrationStatus == ImmigrationStatus.NeedVisaInterviewScheduled ? r.VisaInterviewDate : null,
            isDo ? null : passportCountry,
            isDo ? null : passportNo,
            isDo ? r.SelectedIdType : null,
            isDo ? idNo : null);
        return true;
    }

    private static void Apply(Student student, NormalizedStudent norm)
    {
        student.FirstName = norm.FirstName;
        student.LastName = norm.LastName;
        student.Email = norm.Email;
        student.MobilePhone = norm.MobilePhone;
        student.AcademicStatus = norm.AcademicStatus;
        student.VisaStatus = norm.VisaStatus;
        student.MedicalSchool = norm.MedicalSchool;
        student.MedicalSchoolCountry = norm.MedicalSchoolCountry;
        student.City = norm.City;
        student.State = norm.State;
        student.Status = norm.Status;
        student.StudentOid = norm.StudentOid;
    }

    private const int NeedsItemMaxLength = 120;
    private const int NeedsListMaxCount = 200;

    /// <summary>Trims, drops blanks/oversized items, de-dupes, and caps a title list from a Needs-tab
    /// selection; returns null for an empty result so the column clears cleanly.</summary>
    private static List<string>? CleanTitleList(IReadOnlyList<string>? items)
    {
        if (items is null) return null;
        var cleaned = items
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => s.Length <= NeedsItemMaxLength)
            .Distinct(StringComparer.Ordinal)
            .Take(NeedsListMaxCount)
            .ToList();
        return cleaned.Count == 0 ? null : cleaned;
    }

    private static string? TrimmedOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private const int ScoreMaxLength = 16;
    private const int EducationTextMaxLength = 200;
    private const int MinAttempts = 1;
    private const int MaxAttempts = 10;

    /// <summary>Validated, normalized Education-tab values ready to persist. Exam sub-fields are already
    /// null-ed to match their step status (score/attempts only when Taken, date only when WillTake).</summary>
    private sealed record NormalizedEducation(
        string? MedicalSchool, string? MedicalSchoolCountry, DateOnly? GraduationDate,
        ExamStatus? UsmleStep1, string? UsmleScore1, int? UsmleAttempts1, DateOnly? UsmleDate1,
        ExamStatus? UsmleStep2, string? UsmleScore2, int? UsmleAttempts2, DateOnly? UsmleDate2,
        ExamStatus? UsmleStep3, string? UsmleScore3, int? UsmleAttempts3, DateOnly? UsmleDate3,
        bool? EcfmgCertified, bool? AppliedMatch,
        bool? ComlexLevel1Taken, bool? ComlexLevel1Passed,
        ExamStatus? ComlexLevel2, string? ComlexLevel2Score, int? ComlexLevel2Attempts, DateOnly? ComlexLevel2Date,
        ExamStatus? ComlexLevel3, string? ComlexLevel3Score, int? ComlexLevel3Attempts, DateOnly? ComlexLevel3Date,
        string? Undergrad, EducationYear? EducationYear, bool? IsAmsa, string? Association, bool? IsLeadership,
        bool? IsToefl, bool? IsIndbe);

    private static bool TryNormalizeEducation(
        UpdateStudentEducationRequest r, out NormalizedEducation norm, out string error)
    {
        norm = null!;
        error = string.Empty;

        // Enums must be in range if supplied.
        if (r.UsmleStep1 is { } s1 && !Enum.IsDefined(s1)) { error = "UsmleStep1 is invalid."; return false; }
        if (r.UsmleStep2 is { } s2 && !Enum.IsDefined(s2)) { error = "UsmleStep2 is invalid."; return false; }
        if (r.UsmleStep3 is { } s3 && !Enum.IsDefined(s3)) { error = "UsmleStep3 is invalid."; return false; }
        if (r.ComlexLevel2 is { } c2 && !Enum.IsDefined(c2)) { error = "ComlexLevel2 is invalid."; return false; }
        if (r.ComlexLevel3 is { } c3 && !Enum.IsDefined(c3)) { error = "ComlexLevel3 is invalid."; return false; }
        if (r.EducationYear is { } ey && !Enum.IsDefined(ey)) { error = "EducationYear is invalid."; return false; }

        // Attempts, when supplied, must be within 1–10 (legacy dropdown range).
        if (!TryAttempts(r.UsmleAttempts1, "UsmleAttempts1", out error)) return false;
        if (!TryAttempts(r.UsmleAttempts2, "UsmleAttempts2", out error)) return false;
        if (!TryAttempts(r.UsmleAttempts3, "UsmleAttempts3", out error)) return false;
        if (!TryAttempts(r.ComlexLevel2Attempts, "ComlexLevel2Attempts", out error)) return false;
        if (!TryAttempts(r.ComlexLevel3Attempts, "ComlexLevel3Attempts", out error)) return false;

        if (!TryOptional(r.MedicalSchool, SchoolMaxLength, "MedicalSchool", out var school, out error)) return false;
        if (!TryOptional(r.MedicalSchoolCountry, CountryMaxLength, "MedicalSchoolCountry", out var country, out error)) return false;
        if (!TryOptional(r.UsmleScore1, ScoreMaxLength, "UsmleScore1", out var score1, out error)) return false;
        if (!TryOptional(r.UsmleScore2, ScoreMaxLength, "UsmleScore2", out var score2, out error)) return false;
        if (!TryOptional(r.UsmleScore3, ScoreMaxLength, "UsmleScore3", out var score3, out error)) return false;
        if (!TryOptional(r.ComlexLevel2Score, ScoreMaxLength, "ComlexLevel2Score", out var cScore2, out error)) return false;
        if (!TryOptional(r.ComlexLevel3Score, ScoreMaxLength, "ComlexLevel3Score", out var cScore3, out error)) return false;
        if (!TryOptional(r.Undergrad, EducationTextMaxLength, "Undergrad", out var undergrad, out error)) return false;
        if (!TryOptional(r.Association, EducationTextMaxLength, "Association", out var association, out error)) return false;

        // Null-out exam sub-fields to match the reported status server-side (don't trust the client): a step
        // that was Taken keeps its score/attempts and drops the scheduled date; one that WillTake keeps only
        // the date; NoPlan / unset keeps nothing. Mirrors the tab's own conditional rendering.
        var (u1s, u1a, u1d) = ExamSubFields(r.UsmleStep1, score1, r.UsmleAttempts1, r.UsmleDate1);
        var (u2s, u2a, u2d) = ExamSubFields(r.UsmleStep2, score2, r.UsmleAttempts2, r.UsmleDate2);
        var (u3s, u3a, u3d) = ExamSubFields(r.UsmleStep3, score3, r.UsmleAttempts3, r.UsmleDate3);
        var (c2s, c2a, c2d) = ExamSubFields(r.ComlexLevel2, cScore2, r.ComlexLevel2Attempts, r.ComlexLevel2Date);
        var (c3s, c3a, c3d) = ExamSubFields(r.ComlexLevel3, cScore3, r.ComlexLevel3Attempts, r.ComlexLevel3Date);

        norm = new NormalizedEducation(
            school, country, r.GraduationDate,
            r.UsmleStep1, u1s, u1a, u1d,
            r.UsmleStep2, u2s, u2a, u2d,
            r.UsmleStep3, u3s, u3a, u3d,
            r.EcfmgCertified, r.AppliedMatch,
            r.ComlexLevel1Taken,
            // "How did you do?" only applies when Level 1 was actually taken.
            r.ComlexLevel1Taken == true ? r.ComlexLevel1Passed : null,
            r.ComlexLevel2, c2s, c2a, c2d,
            r.ComlexLevel3, c3s, c3a, c3d,
            undergrad, r.EducationYear, r.IsAmsa, association, r.IsLeadership,
            r.IsToefl, r.IsIndbe);
        return true;
    }

    private static bool TryAttempts(int? value, string field, out string error)
    {
        error = string.Empty;
        if (value is { } a && (a < MinAttempts || a > MaxAttempts))
        {
            error = $"{field} must be between {MinAttempts} and {MaxAttempts}.";
            return false;
        }

        return true;
    }

    /// <summary>Keeps only the exam sub-fields that the given status justifies (Taken → score+attempts;
    /// WillTake → scheduled date; NoPlan/null → nothing).</summary>
    private static (string? Score, int? Attempts, DateOnly? Date) ExamSubFields(
        ExamStatus? status, string? score, int? attempts, DateOnly? date) => status switch
    {
        ExamStatus.Taken => (score, attempts, null),
        ExamStatus.WillTake => (null, null, date),
        _ => (null, null, null),
    };

    private static StudentDetailResponse ToDetail(Student x) =>
        new(x.Id, x.FirstName, x.LastName, x.Email, x.MobilePhone, x.AcademicStatus, x.VisaStatus,
            x.MedicalSchool, x.MedicalSchoolCountry, x.City, x.State, x.Status, x.StudentOid,
            x.Birthdate, x.Gender, x.ImmigrationStatus, x.ImmigrationStatusOther, x.VisaInterviewDate,
            x.PassportIssuedCountry, x.PassportNumber, x.SelectedIdType, x.IdNumber, x.AvatarBlobName,
            x.Interests, x.PreferredSpecialty, x.SpecialtyLocations, x.CustomSpecialtyLocation, x.Importants,
            x.GraduationDate,
            x.UsmleStep1, x.UsmleScore1, x.UsmleAttempts1, x.UsmleDate1,
            x.UsmleStep2, x.UsmleScore2, x.UsmleAttempts2, x.UsmleDate2,
            x.UsmleStep3, x.UsmleScore3, x.UsmleAttempts3, x.UsmleDate3,
            x.EcfmgCertified, x.AppliedMatch,
            x.ComlexLevel1Taken, x.ComlexLevel1Passed,
            x.ComlexLevel2, x.ComlexLevel2Score, x.ComlexLevel2Attempts, x.ComlexLevel2Date,
            x.ComlexLevel3, x.ComlexLevel3Score, x.ComlexLevel3Attempts, x.ComlexLevel3Date,
            x.Undergrad, x.EducationYear, x.IsAmsa, x.Association, x.IsLeadership,
            x.IsToefl, x.IsIndbe);

    // Lightweight summary for form pickers (the /options list): identity core only, rollups zeroed —
    // pickers show a name, not the achievements columns. The paginated directory list projects the
    // rollups inline (correlated subqueries) instead of calling this.
    private static StudentSummaryResponse ToSummary(Student x) =>
        new(x.Id, x.FirstName + " " + x.LastName, x.Email, x.MobilePhone,
            x.AcademicStatus, x.VisaStatus, x.City, x.State, x.Status, 0m, 0m, 0, 0);
}
