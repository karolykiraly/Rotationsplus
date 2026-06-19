using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Admin-initiated refund of a rotation's captured payments. A refund is a money action, so it is NOT a
/// plain status edit — the admin edit form's PUT rejects a direct move to <see cref="RotationStatus.Refunded"/>
/// and routes the admin here instead. This refunds every Succeeded payment on the rotation through the
/// gateway and moves the booking to Refunded, but only from a state where that's a legal transition
/// (Cancelled or Completed), so a still-active or unpaid booking can't be refunded by mistake.
/// </summary>
public static class PaymentRefundEndpoints
{
    public static IEndpointRouteBuilder MapPaymentRefundEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/rotations/{id:guid}/refund", async (
            Guid id, RotationsDbContext db, IPaymentGateway gateway, CancellationToken cancellationToken) =>
        {
            var rotation = await db.Rotations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rotation is null)
            {
                return Results.NotFound();
            }

            // Refunded is reachable only from Cancelled/Completed (RotationStatusMachine). This both gates
            // intent and makes a repeat refund a 409 — Refunded is terminal, so the second call can't transition.
            if (!RotationStatusMachine.CanTransition(rotation.Status, RotationStatus.Refunded))
            {
                return Results.Conflict($"A {rotation.Status} rotation can't be refunded.");
            }

            // IgnoreQueryFilters so a soft-deleted-but-captured payment is still reconciled, mirroring the
            // webhook fulfilment's "money has changed hands, never black-hole the row" stance.
            var captured = await db.Payments
                .IgnoreQueryFilters()
                .Where(p => p.RotationId == id && p.Status == PaymentStatus.Succeeded)
                .ToListAsync(cancellationToken);
            if (captured.Count == 0)
            {
                return Results.Conflict("This rotation has no captured payment to refund.");
            }

            var refundableStatus = rotation.Status;

            // The DbContext has EnableRetryOnFailure configured, so a user-initiated transaction must run
            // inside the execution strategy — that way the whole claim→refund→commit unit is retried as one
            // atomic operation on a transient failure (the gateway refund is idempotent per key, so a retry
            // is safe). A bare BeginTransactionAsync throws under the retrying strategy.
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                // Atomically CLAIM the refund: flip the rotation out of its (refundable) current status with a
                // conditional UPDATE. The UPDATE takes a row lock, so two concurrent refunds serialise — the
                // winner updates 1 row, the loser updates 0 (the status no longer matches) and 409s. This makes
                // the gateway refund + payment writes below run EXACTLY ONCE, instead of relying on provider-side
                // idempotency as the only guard against a double money-out (the same reason the deposit has a DB
                // uniqueness guard rather than trusting the gateway alone).
                var claimed = await db.Rotations
                    .Where(r => r.Id == id && r.Status == refundableStatus)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RotationStatus.Refunded), cancellationToken);
                if (claimed == 0)
                {
                    return Results.Conflict("This rotation has already been refunded.");
                }

                foreach (var payment in captured)
                {
                    // ProviderPaymentIntentId is always set on a Succeeded payment (it's how the webhook matched
                    // it). Key the refund to the payment so a retry returns the same provider refund.
                    var refund = await gateway.RefundAsync(payment.ProviderPaymentIntentId!, $"refund-{payment.Id}", cancellationToken);
                    payment.Status = PaymentStatus.Refunded;
                    payment.ProviderRefundId = refund.RefundId;
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Results.Ok(new RefundResponse(id, RotationStatus.Refunded, captured.Count));
            });
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("RefundRotation")
        .WithTags("Payments");

        return routes;
    }
}

/// <summary>The outcome of a refund: the rotation's new status and how many payments were refunded.</summary>
public sealed record RefundResponse(Guid RotationId, RotationStatus Status, int PaymentsRefunded);
