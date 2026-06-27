using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Documents;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

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
            RotationStatus? status, Guid? programId, string? q, string? scope, int? page, int? pageSize,
            RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!PaginationExtensions.TryBuildSearchPattern(q, out var pattern, out var searchError))
            {
                return Results.BadRequest(searchError);
            }

            var query = db.Rotations.AsQueryable();
            // The admin screen splits the list into Current (non-terminal lifecycle) and Historical
            // (terminal) sections; `scope` selects one. Legacy parity: Current ≈ Pending/Approved/Active/
            // To-be-evaluated, Historical ≈ Completed/Cancelled/Refunded/Abandoned/Rejected.
            if (string.Equals(scope, "current", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => CurrentScopeStatuses.Contains(r.Status));
            else if (string.Equals(scope, "historical", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => !CurrentScopeStatuses.Contains(r.Status));
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
                    r.Status,
                    r.Program.RetailAmountPerWeek * r.Weeks, // retail cost of the booking (per-week × weeks)
                    // "Needs Visa" — true when the booked directory student needs visa help. Correlated
                    // EXISTS subquery (the rotation has no Student navigation; StudentId may be null).
                    db.Students.Any(s => s.Id == r.StudentId && s.VisaStatus == VisaStatus.NeedsVisaHelp)))
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
            if (program is null)
            {
                return Results.NotFound();
            }
            var paidAmount = await PaidAmountAsync(db, rotation.Id, cancellationToken);
            return Results.Ok(ToDetail(rotation, program, paidAmount));
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

            if (!TryValidateMoney(program, WeeksBetween(request.StartDate, request.EndDate), out var moneyError))
            {
                return Results.BadRequest(moneyError);
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

            // A brand-new booking has no captured payments yet → Paid Amount 0.
            return Results.Created($"/api/rotations/{rotation.Id}", ToDetail(rotation, program, 0m));
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

            if (!TryValidateMoney(program, WeeksBetween(request.StartDate, request.EndDate), out var moneyError))
            {
                return Results.BadRequest(moneyError);
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

            var paidAmount = await PaidAmountAsync(db, rotation.Id, cancellationToken);
            return Results.Ok(ToDetail(rotation, program, paidAmount));
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

    /// <summary>The non-terminal lifecycle statuses shown in the "Current Rotations" section. The
    /// "Historical Rotations" section is their complement (the terminal states Completed/Cancelled/
    /// Refunded/Abandoned/Rejected). Mirrors the legacy current-vs-history split.</summary>
    private static readonly RotationStatus[] CurrentScopeStatuses =
        [RotationStatus.Pending, RotationStatus.NotStarted, RotationStatus.Active, RotationStatus.ToBeEvaluated];

    /// <summary>Program facts needed to flatten a rotation into its response, plus the per-week money
    /// figures (used to validate that the booking's totals can't overflow their money columns).</summary>
    private sealed record ProgramInfo(
        int ProgramNumber, string SpecialtyName, ProgramType ProgramType, string? PreceptorName,
        decimal RetailAmountPerWeek, decimal WeeklyHonorarium);

    /// <summary>The directory student's identity, snapshotted onto the rotation on write.</summary>
    private sealed record StudentInfo(string Name, string Email, string? Oid);

    private static Task<ProgramInfo?> ResolveProgramAsync(RotationsDbContext db, Guid programId, CancellationToken cancellationToken) =>
        db.Programs
            .Where(p => p.Id == programId)
            .Select(p => new ProgramInfo(
                p.ProgramNumber,
                p.Specialty.Name,
                p.ProgramType,
                p.Preceptor != null ? p.Preceptor.FirstName + " " + p.Preceptor.LastName : null,
                p.RetailAmountPerWeek,
                p.WeeklyHonorarium))
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

    /// <summary>Upper bound on a rotation's duration (mirrors <c>CustomerRotationEndpoints.MaxWeeks</c>) —
    /// a sanity bound against a fat-fingered date range (e.g. a mistyped year). The actual money-overflow
    /// guard is <see cref="TryValidateMoney"/>, which checks the per-week × weeks PRODUCTS; the week cap
    /// alone is insufficient because a high weekly figure can overflow with very few weeks.</summary>
    private const int MaxWeeks = 520;

    /// <summary>The <c>numeric(10,2)</c> ceiling shared by the payment (deposit/total) and honorarium
    /// amount columns. A booking whose per-week figure × weeks exceeds this would overflow at fulfilment
    /// — and the honorarium overflow happens INSIDE the webhook transaction, wedging a paid booking — so
    /// we reject it here, before any money is computed.</summary>
    private const decimal MaxMoney = 99_999_999.99m;

    private static bool TryValidateDates(DateOnly startDate, DateOnly endDate, RotationStatus status, out string error)
    {
        error = string.Empty;

        if (!Enum.IsDefined(status)) { error = "Status is invalid."; return false; }
        if (endDate <= startDate) { error = "EndDate must be after StartDate."; return false; }
        if (WeeksBetween(startDate, endDate) > MaxWeeks)
        {
            error = $"A rotation can span at most {MaxWeeks} weeks.";
            return false;
        }

        return true;
    }

    /// <summary>Rejects a booking whose total deposit price (retail × weeks) or total honorarium (weekly
    /// honorarium × weeks) would exceed the money columns' <see cref="MaxMoney"/> ceiling — closing the
    /// overflow that would otherwise throw inside the deposit-fulfilment transaction.</summary>
    private static bool TryValidateMoney(ProgramInfo program, int weeks, out string error)
    {
        error = string.Empty;
        if (program.RetailAmountPerWeek * weeks > MaxMoney || program.WeeklyHonorarium * weeks > MaxMoney)
        {
            error = $"This program's per-week amounts over {weeks} week(s) exceed the maximum supported total.";
            return false;
        }

        return true;
    }

    /// <summary>Sum of the rotation's captured (Succeeded) payments — the Selected Rotation panel's
    /// "Paid Amount". A refund flips the payment to Refunded, so it correctly drops out of the total.</summary>
    private static async Task<decimal> PaidAmountAsync(RotationsDbContext db, Guid rotationId, CancellationToken cancellationToken) =>
        await db.Payments
            .Where(p => p.RotationId == rotationId && p.Status == PaymentStatus.Succeeded)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

    private static RotationDetailResponse ToDetail(Rotation r, ProgramInfo program, decimal paidAmount) =>
        new(r.Id, r.RotationNumber, r.ProgramId, program.SpecialtyName, program.ProgramType, program.PreceptorName,
            r.StudentId, r.StudentName, r.StudentEmail, r.StudentOid, r.StartDate, r.EndDate, r.Weeks, r.Status,
            program.ProgramNumber, program.RetailAmountPerWeek * r.Weeks, paidAmount,
            RotationStatusMachine.NextFrom(r.Status));
}
