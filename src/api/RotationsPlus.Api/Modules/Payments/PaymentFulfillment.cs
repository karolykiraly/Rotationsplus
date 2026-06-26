using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// The single money-fulfilment path, shared by the real provider webhook
/// (<see cref="PaymentWebhookEndpoints"/>) and the DEV-only simulation endpoint
/// (<see cref="PaymentDevEndpoints"/>). Keeping one implementation means the deposit→approval logic is
/// identical whether a real Stripe event or a developer's "simulate" click drives it — no divergence
/// between what we test on DEV and what runs in PROD. The caller owns the transaction (idempotency
/// ledger + SaveChanges); this only mutates the tracked entities.
/// </summary>
internal static class PaymentFulfillment
{
    /// <summary>
    /// Applies a verified payment event to the matching <see cref="Payment"/>. Acts only on a still-Pending
    /// payment (so a re-delivered or duplicate event is a no-op), and on success approves the booking via
    /// the rotation state machine. Does not save — the caller commits.
    /// </summary>
    public static async Task FulfillAsync(RotationsDbContext db, PaymentWebhookEvent paymentEvent, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters so a payment that's been soft-deleted is still found and fulfilled rather
        // than silently slipping past the global filter — money has changed hands, so we must reconcile
        // the row, not black-hole it. The status guards below still gate what we actually do with it.
        // (No code path soft-deletes a payment today; if one is added, give Payment an optimistic-
        // concurrency token so a delete racing this fulfilment is detected rather than silently merged.)
        var payment = await db.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.ProviderPaymentIntentId == paymentEvent.PaymentIntentId, cancellationToken);

        // Unknown intent (or one not ours) → nothing to fulfil; the caller still records the event and 200s
        // so the provider stops retrying.
        if (payment is null)
        {
            return;
        }

        switch (paymentEvent.Type)
        {
            case PaymentWebhookEventTypes.PaymentSucceeded when payment.Status == PaymentStatus.Pending:
                payment.Status = PaymentStatus.Succeeded;
                await ApproveRotationAsync(db, payment.RotationId, cancellationToken);
                // The deposit is confirmed → generate the preceptor's three-stage payout schedule (idempotent,
                // so a re-delivered success event won't double-schedule). Mirrors the legacy paid-booking trigger.
                await HonorariumGenerator.EnsureForRotationAsync(db, payment.RotationId, cancellationToken);
                break;

            case PaymentWebhookEventTypes.PaymentFailed when payment.Status == PaymentStatus.Pending:
                payment.Status = PaymentStatus.Failed;
                break;

            // Any other event type, or a payment already in a terminal state, is a no-op.
        }
    }

    /// <summary>On a successful deposit, move the booking from Pending to NotStarted ("Approved") — but
    /// only if that's a legal transition, so a rotation an admin has since cancelled/rejected is left alone.</summary>
    private static async Task ApproveRotationAsync(RotationsDbContext db, Guid rotationId, CancellationToken cancellationToken)
    {
        var rotation = await db.Rotations.FirstOrDefaultAsync(r => r.Id == rotationId, cancellationToken);
        if (rotation is not null && RotationStatusMachine.CanTransition(rotation.Status, RotationStatus.NotStarted))
        {
            rotation.Status = RotationStatus.NotStarted;
        }
    }
}
