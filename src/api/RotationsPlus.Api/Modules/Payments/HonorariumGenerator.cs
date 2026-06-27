using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Generates a rotation's three-stage preceptor payout schedule. Called from
/// <see cref="PaymentFulfillment"/> when a deposit succeeds (the rotation is confirmed and money is in) —
/// the same trigger the legacy system used (honorariums created at the paid-booking event). The total is
/// program weekly honorarium × weeks, split 25% / 25% / 50% across Deposit / Start / Evaluation. Like the
/// rest of fulfilment, this only mutates tracked entities; the caller owns the transaction and SaveChanges.
/// </summary>
internal static class HonorariumGenerator
{
    /// <summary>The <c>numeric(10,2)</c> ceiling of the honorarium Amount column — a total above this
    /// would overflow on insert (see the guard in <see cref="EnsureForRotationAsync"/>).</summary>
    private const decimal MaxMoney = 99_999_999.99m;

    /// <summary>Stage split (legacy parity): 25% deposit, 25% start, 50% evaluation.</summary>
    private static readonly (HonorariumStage Stage, decimal Fraction)[] Stages =
    [
        (HonorariumStage.Deposit, 0.25m),
        (HonorariumStage.Start, 0.25m),
        (HonorariumStage.Evaluation, 0.50m),
    ];

    /// <summary>
    /// Creates the three payout rows for <paramref name="rotationId"/> if they don't already exist.
    /// Idempotent: a re-delivered deposit event (or a manual re-run) finds the rows present and no-ops, so
    /// fulfilment never double-schedules. Skips quietly when the rotation/program is missing or the program
    /// carries no weekly honorarium (nothing to pay).
    /// </summary>
    public static async Task EnsureForRotationAsync(RotationsDbContext db, Guid rotationId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters so soft-deleted rows still count as "already generated" — we never resurrect a
        // schedule, and this is the partner to the unique (RotationId, Stage) live-row index backstop.
        var alreadyGenerated = await db.Honorariums
            .IgnoreQueryFilters()
            .AnyAsync(h => h.RotationId == rotationId, cancellationToken);
        if (alreadyGenerated)
        {
            return;
        }

        var rotation = await db.Rotations
            .Where(r => r.Id == rotationId)
            .Select(r => new { r.Id, r.RotationNumber, r.StartDate, r.EndDate, r.StudentName, r.Weeks, r.ProgramId })
            .FirstOrDefaultAsync(cancellationToken);
        if (rotation is null)
        {
            return;
        }

        var program = await db.Programs
            .Where(p => p.Id == rotation.ProgramId)
            .Select(p => new
            {
                p.WeeklyHonorarium,
                p.PreceptorId,
                PreceptorName = p.Preceptor != null ? p.Preceptor.FirstName + " " + p.Preceptor.LastName : null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // No program or no honorarium rate → no payout schedule to generate.
        if (program is null || program.WeeklyHonorarium <= 0)
        {
            return;
        }

        var total = decimal.Round(program.WeeklyHonorarium * rotation.Weeks, 2, MidpointRounding.AwayFromZero);

        // Evaluation-tab due date snapshot: rotation end date + the legacy 7-day grace (end_date + 7d).
        var evaluationDueDate = rotation.EndDate.AddDays(7);

        // Defense-in-depth: a total that would overflow the Amount column's numeric(10,2) ceiling is NOT
        // inserted — this runs inside the deposit-fulfilment transaction, so an overflow here would throw
        // and 500-loop the webhook, wedging a paid booking. The rotation create/update path already rejects
        // such a booking up front (RotationEndpoints.TryValidateMoney); this is the last-line guard for any
        // other creation path, trading a (legitimately absurd) missing payout schedule for a safe webhook.
        if (total > MaxMoney)
        {
            return;
        }

        // Allocate to the cent so the three stages sum to the total EXACTLY — the last stage takes the
        // remainder rather than its rounded fraction, absorbing any rounding drift (money must be exact).
        decimal allocated = 0m;
        for (var i = 0; i < Stages.Length; i++)
        {
            var (stage, fraction) = Stages[i];
            var amount = i == Stages.Length - 1
                ? total - allocated
                : decimal.Round(total * fraction, 2, MidpointRounding.AwayFromZero);
            allocated += amount;

            db.Honorariums.Add(new Honorarium
            {
                RotationId = rotation.Id,
                PreceptorId = program.PreceptorId,
                PreceptorName = program.PreceptorName ?? "—",
                StudentName = rotation.StudentName,
                RotationNumber = rotation.RotationNumber,
                RotationStartDate = rotation.StartDate,
                EvaluationDueDate = evaluationDueDate,
                Stage = stage,
                Amount = amount,
                Currency = PricingService.Currency,
                Status = HonorariumStatus.Pending,
            });
        }
    }
}
