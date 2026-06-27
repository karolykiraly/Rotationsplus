using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
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
                .Select(x => ToSummary(x))
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

    private static StudentDetailResponse ToDetail(Student x) =>
        new(x.Id, x.FirstName, x.LastName, x.Email, x.MobilePhone, x.AcademicStatus, x.VisaStatus,
            x.MedicalSchool, x.MedicalSchoolCountry, x.City, x.State, x.Status, x.StudentOid);

    private static StudentSummaryResponse ToSummary(Student x) =>
        new(x.Id, x.FirstName + " " + x.LastName, x.Email, x.MobilePhone,
            x.AcademicStatus, x.VisaStatus, x.City, x.State, x.Status);
}
