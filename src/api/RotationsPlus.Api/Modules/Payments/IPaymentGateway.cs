namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Abstraction over the payment provider (Stripe), so the domain depends on an interface and the real
/// SDK is a swappable adapter — and so DEV/tests run against a deterministic fake with no live keys
/// (Plan_Migration §3: isolated vendor sandboxes; PROD switches to the real account at cutover). All
/// amounts cross this boundary in the provider's minor units (cents) to match Stripe's integer money.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Opens a payment intent for <paramref name="amountMinorUnits"/> and returns its id + client
    /// secret (the SPA confirms the card against the secret). <paramref name="idempotencyKey"/> makes
    /// a retried create return the same intent rather than charging twice.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the webhook signature and parses the payload. Returns the event on success, or
    /// <c>null</c> when the signature is missing/invalid (the caller must reject with 400). Verifying
    /// the signature is the webhook's only authentication — the endpoint is otherwise anonymous.
    /// <para>
    /// NOTE (replay window): this verifies signature integrity only, not freshness — a valid past
    /// payload captured in transit could be re-presented and would verify. Two things bound the blast
    /// radius today: transport is HTTPS, and fulfilment is idempotent per event id (a replayed event is
    /// a no-op once its id is in the <see cref="ProcessedWebhookEvent"/> ledger). The real Stripe adapter
    /// MUST additionally enforce the signed timestamp tolerance (Stripe's <c>t=</c> in
    /// <c>Stripe-Signature</c>, ~5 min) and reject stale payloads — the fake gateway has no timestamp, so
    /// that check lives in the real adapter, not the shared contract.
    /// </para>
    /// </summary>
    PaymentWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader);
}

/// <summary>The provider's intent id and the client secret the SPA uses to confirm payment.</summary>
public readonly record struct PaymentIntentResult(string PaymentIntentId, string ClientSecret);

/// <summary>A verified webhook event. <see cref="Type"/> is the provider event type (e.g.
/// <c>payment_intent.succeeded</c>); <see cref="PaymentIntentId"/> ties it back to a <see cref="Payment"/>.</summary>
public sealed record PaymentWebhookEvent(string EventId, string Type, string PaymentIntentId);

/// <summary>Well-known provider webhook event types this system fulfils.</summary>
public static class PaymentWebhookEventTypes
{
    public const string PaymentSucceeded = "payment_intent.succeeded";
    public const string PaymentFailed = "payment_intent.payment_failed";
}
