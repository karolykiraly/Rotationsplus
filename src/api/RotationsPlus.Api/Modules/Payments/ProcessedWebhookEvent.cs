namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// A ledger of provider webhook events we've already processed, keyed by the provider's event id.
/// The webhook handler records each event here inside the same transaction as the fulfilment, so a
/// re-delivered event (providers deliver at-least-once) is recognised and skipped — making fulfilment
/// idempotent. Not an <c>AuditableEntity</c>: it's an append-only technical ledger, not a domain
/// aggregate, and is never soft-deleted.
/// </summary>
public sealed class ProcessedWebhookEvent
{
    /// <summary>The provider's event id (e.g. Stripe <c>evt_…</c>) — the primary key, so a duplicate
    /// insert violates the PK and the handler treats the event as already-seen.</summary>
    public required string Id { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
