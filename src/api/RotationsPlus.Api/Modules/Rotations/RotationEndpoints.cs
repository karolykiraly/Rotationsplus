using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Rotations;

/// <summary>
/// Rotation management endpoints — AdminOnly (Plan_Admin §4 authz matrix). A rotation books a student
/// into a program over a date range with a lifecycle status. Students see their own rotations via the
/// portal in a later slice. The transition state machine, documents, evaluation, and payments arrive
/// later; this slice is admin CRUD over the core booking.
/// </summary>
public static class RotationEndpoints
{
    public static IEndpointRouteBuilder MapRotationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/rotations")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithTags("Rotations");

        group.MapGet("/", async (
            RotationStatus? status, Guid? programId, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.Rotations.AsQueryable();
            if (status is { } s) query = query.Where(r => r.Status == s);
            if (programId is { } pid) query = query.Where(r => r.ProgramId == pid);

            var rotations = await query
                .OrderByDescending(r => r.StartDate)
                .Select(r => new RotationSummaryResponse(
                    r.Id,
                    r.StudentName,
                    r.StudentEmail,
                    r.Program.Specialty.Name,
                    r.Program.ProgramType,
                    r.Program.Preceptor != null ? r.Program.Preceptor.FirstName + " " + r.Program.Preceptor.LastName : null,
                    r.StartDate,
                    r.EndDate,
                    r.Weeks,
                    r.Status))
                .ToListAsync(cancellationToken);

            return Results.Ok(rotations);
        })
        .WithName("ListRotations");

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var rotation = await db.Rotations
                .Where(r => r.Id == id)
                .Select(r => new RotationDetailResponse(
                    r.Id,
                    r.ProgramId,
                    r.Program.Specialty.Name,
                    r.Program.ProgramType,
                    r.Program.Preceptor != null ? r.Program.Preceptor.FirstName + " " + r.Program.Preceptor.LastName : null,
                    r.StudentId,
                    r.StudentName,
                    r.StudentEmail,
                    r.StudentOid,
                    r.StartDate,
                    r.EndDate,
                    r.Weeks,
                    r.Status))
                .FirstOrDefaultAsync(cancellationToken);

            return rotation is null ? Results.NotFound() : Results.Ok(rotation);
        })
        .WithName("GetRotation");

        group.MapPost("/", async (CreateRotationRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidateDates(request.StartDate, request.EndDate, request.Status, out var error))
            {
                return Results.BadRequest(error);
            }

            var program = await ResolveProgramAsync(db, request.ProgramId, cancellationToken);
            if (program is null)
            {
                return Results.BadRequest($"Program '{request.ProgramId}' does not exist.");
            }

            var student = await ResolveStudentAsync(db, request.StudentId, cancellationToken);
            if (student is null)
            {
                return Results.BadRequest($"Student '{request.StudentId}' does not exist.");
            }

            var rotation = new Rotation
            {
                ProgramId = request.ProgramId,
                StudentId = request.StudentId,
                StudentName = student.Name,   // snapshot the directory student's identity at write time
                StudentEmail = student.Email,
                StudentOid = student.Oid,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Weeks = WeeksBetween(request.StartDate, request.EndDate),
                Status = request.Status,
            };
            db.Rotations.Add(rotation);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/rotations/{rotation.Id}", ToDetail(rotation, program));
        })
        .WithName("CreateRotation");

        group.MapPut("/{id:guid}", async (Guid id, UpdateRotationRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidateDates(request.StartDate, request.EndDate, request.Status, out var error))
            {
                return Results.BadRequest(error);
            }

            var rotation = await db.Rotations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rotation is null)
            {
                return Results.NotFound();
            }

            var program = await ResolveProgramAsync(db, request.ProgramId, cancellationToken);
            if (program is null)
            {
                return Results.BadRequest($"Program '{request.ProgramId}' does not exist.");
            }

            var student = await ResolveStudentAsync(db, request.StudentId, cancellationToken);
            if (student is null)
            {
                return Results.BadRequest($"Student '{request.StudentId}' does not exist.");
            }

            rotation.ProgramId = request.ProgramId;
            rotation.StudentId = request.StudentId;
            rotation.StudentName = student.Name;   // re-snapshot from the (possibly changed) directory student
            rotation.StudentEmail = student.Email;
            rotation.StudentOid = student.Oid;
            rotation.StartDate = request.StartDate;
            rotation.EndDate = request.EndDate;
            rotation.Weeks = WeeksBetween(request.StartDate, request.EndDate);
            rotation.Status = request.Status;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToDetail(rotation, program));
        })
        .WithName("UpdateRotation");

        group.MapDelete("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var rotation = await db.Rotations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rotation is null)
            {
                return Results.NotFound();
            }

            db.Rotations.Remove(rotation); // interceptor converts the delete into a soft-delete
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteRotation");

        return routes;
    }

    /// <summary>Program facts needed to flatten a rotation into its response.</summary>
    private sealed record ProgramInfo(string SpecialtyName, ProgramType ProgramType, string? PreceptorName);

    /// <summary>The directory student's identity, snapshotted onto the rotation on write.</summary>
    private sealed record StudentInfo(string Name, string Email, string? Oid);

    private static Task<ProgramInfo?> ResolveProgramAsync(RotationsDbContext db, Guid programId, CancellationToken cancellationToken) =>
        db.Programs
            .Where(p => p.Id == programId)
            .Select(p => new ProgramInfo(
                p.Specialty.Name,
                p.ProgramType,
                p.Preceptor != null ? p.Preceptor.FirstName + " " + p.Preceptor.LastName : null))
            .FirstOrDefaultAsync(cancellationToken);

    private static Task<StudentInfo?> ResolveStudentAsync(RotationsDbContext db, Guid studentId, CancellationToken cancellationToken) =>
        db.Students
            .Where(s => s.Id == studentId)
            .Select(s => new StudentInfo(s.FirstName + " " + s.LastName, s.Email, s.StudentOid))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Weeks spanned by the date range, rounding a partial week UP, at least 1 (the range is
    /// already validated end &gt; start). Rounding up is deliberate: <c>Weeks</c> will feed per-week
    /// pricing later, so a partial week must bill as a full week rather than be silently dropped.
    /// Exact multiples are unaffected (28 days → 4, 42 days → 6).</summary>
    private static int WeeksBetween(DateOnly start, DateOnly end) =>
        Math.Max(1, (end.DayNumber - start.DayNumber + 6) / 7);

    private static bool TryValidateDates(DateOnly startDate, DateOnly endDate, RotationStatus status, out string error)
    {
        error = string.Empty;

        if (!Enum.IsDefined(status)) { error = "Status is invalid."; return false; }
        if (endDate <= startDate) { error = "EndDate must be after StartDate."; return false; }

        return true;
    }

    private static RotationDetailResponse ToDetail(Rotation r, ProgramInfo program) =>
        new(r.Id, r.ProgramId, program.SpecialtyName, program.ProgramType, program.PreceptorName,
            r.StudentId, r.StudentName, r.StudentEmail, r.StudentOid, r.StartDate, r.EndDate, r.Weeks, r.Status);
}
