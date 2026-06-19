using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// A single payment taken against a <see cref="Rotation"/> — today the booking deposit. The amount
/// charged now (<see cref="Amount"/>, the deposit) is computed server-side by <c>PricingService</c>;
/// <see cref="TotalAmount"/>/<see cref="OutstandingAmount"/> record the full price and the remainder
/// billed later. The payment is created <see cref="PaymentStatus.Pending"/> when the provider intent
/// is opened and is moved to its terminal state by the provider webhook (fulfilment), never by the
/// browser — this closes the legacy "browser-callback fulfilment" silent-loss gap.
/// </summary>
public sealed class Payment : AuditableEntity
{
    public required Guid RotationId { get; set; }
    public Rotation Rotation { get; set; } = null!;

    public required decimal Amount { get; set; }
    public required decimal TotalAmount { get; set; }
    public required decimal OutstandingAmount { get; set; }
    public required string Currency { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>The payment provider's intent id (e.g. Stripe <c>pi_…</c>). Unique — the webhook looks
    /// the payment up by it. Null only in the brief window before the gateway returns.</summary>
    public string? ProviderPaymentIntentId { get; set; }

    /// <summary>The idempotency key sent to the gateway when creating the intent, derived from the
    /// payment id, so a retried create returns the same intent instead of charging twice.</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>The provider's refund id once this payment has been refunded (set alongside
    /// <see cref="PaymentStatus.Refunded"/>); null while the payment is live. Kept for reconciliation
    /// against the provider's refund records.</summary>
    public string? ProviderRefundId { get; set; }
}
