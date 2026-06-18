namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Payment provider configuration (bound from the <c>Payments</c> config section). On DEV/test these
/// are dev values; the real Stripe secret + webhook signing secret come from Key Vault at PROD cutover.
/// </summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Default DEV/test webhook secret, used when config supplies none. PROD overrides this
    /// from Key Vault. Exposed so tests can sign payloads the same way the fake gateway verifies them.</summary>
    public const string DefaultWebhookSecret = "dev-webhook-secret";

    /// <summary>Shared secret used to verify webhook signatures. The fake gateway signs/verifies an
    /// HMAC-SHA256 over the raw body with this; the real Stripe adapter passes it to Stripe's verifier.</summary>
    public string WebhookSecret { get; set; } = DefaultWebhookSecret;
}
