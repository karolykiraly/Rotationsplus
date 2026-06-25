using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Documents;
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
            RotationStatus? status, Guid? programId, string? q, int? page, int? pageSize,
            RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!PaginationExtensions.TryBuildSearchPattern(q, out var pattern, out var searchError))
            {
                return Results.BadRequest(searchError);
            }

            var query = db.Rotations.AsQueryable();
            if (status is { } s) query = query.Where(r => r.Status == s);
            if (programId is { } pid) query = query.Where(r => r.ProgramId == pid);
            if (pattern is not null)
            {
                // Mirrors the old client-side search: rotation number (with/without the "R" prefix), student
                // name/email, preceptor name, and specialty. The number match strips a leading R from the
                // RAW term (not by slicing the escaped pattern) so "R1001" and "1001" both hit. ILIKE =
                // case-insensitive contains.
                var term = q!.Trim();
                var numberTerm = term.StartsWith("R", StringComparison.OrdinalIgnoreCase) ? term[1..] : term;
                var numberPattern = PaginationExtensions.EscapeLike(numberTerm);
                query = query.Where(r =>
                    EF.Functions.ILike(r.StudentName, pattern) ||
                    EF.Functions.ILike(r.StudentEmail, pattern) ||
                    EF.Functions.ILike(r.Program.Specialty.Name, pattern) ||
                    (r.Program.Preceptor != null
                        && EF.Functions.ILike(r.Program.Preceptor.FirstName + " " + r.Program.Preceptor.LastName, pattern)) ||
                    EF.Functions.ILike(r.RotationNumber.ToString()!, numberPattern));
            }

            var rotations = await query
                .OrderByDescending(r => r.StartDate)
                .ThenByDescending(r => r.RotationNumber) // tie-break so paging is deterministic across pages
                .Select(r => new RotationSummaryResponse(
                    r.Id,
                    r.RotationNumber,
                    r.StudentName,
                    r.StudentEmail,
                    r.Program.Specialty.Name,
                    r.Program.ProgramType,
                    r.Program.Preceptor != null ? r.Program.Preceptor.FirstName + " " + r.Program.Preceptor.LastName : null,
                    r.StartDate,
                    r.EndDate,
                    r.Weeks,
                    r.Status))
                .ToPagedResponseAsync(page, pageSize, cancellationToken); // Normalize() owns the defaulting + caps

            return Results.Ok(rotations);
        })
        .WithName("ListRotations");

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            // Read-only GET — no change tracking needed (this entity is only mapped to a DTO, never saved).
            var rotation = await db.Rotations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rotation is null)
            {
                return Results.NotFound();
            }

            // A live rotation always has a live program (the program-delete guard blocks orphaning, and
            // there's no rotation un-delete). Guard with NotFound anyway rather than null-forgive, so a
            // future invariant break can't turn into a 500. ToDetail also computes the status transitions.
            var program = await ResolveProgramAsync(db, rotation.ProgramId, cancellationToken);
            return program is null ? Results.NotFound() : Results.Ok(ToDetail(rotation, program));
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
            // Materialize the program's required documents for the new booking (same SaveChanges) so the
            // student sees their document checklist as soon as the admin books them.
            await RotationDocumentMaterializer.MaterializeAsync(db, rotation, cancellationToken);
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

            // Refunding is a money action, not a plain status edit — it must move money via the gateway.
            // Block a direct edit to Refunded (even though the state machine permits the transition) and
            // route the admin to POST /api/rotations/{id}/refund instead.
            if (request.Status == RotationStatus.Refunded && rotation.Status != RotationStatus.Refunded)
            {
                return Results.BadRequest("Use the refund action to refund a rotation's deposit.");
            }

            // Enforce the lifecycle state machine: the status may stay the same or move along an allowed
            // edge, but can't jump illegally (e.g. Completed → Pending). Checked against the CURRENT status.
            if (!RotationStatusMachine.CanTransition(rotation.Status, request.Status))
            {
                return Results.BadRequest($"Can't change a rotation from {rotation.Status} to {request.Status}.");
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
        new(r.Id, r.RotationNumber, r.ProgramId, program.SpecialtyName, program.ProgramType, program.PreceptorName,
            r.StudentId, r.StudentName, r.StudentEmail, r.StudentOid, r.StartDate, r.EndDate, r.Weeks, r.Status,
            RotationStatusMachine.NextFrom(r.Status));
}
