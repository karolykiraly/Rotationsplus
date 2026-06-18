using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// A deterministic, keyless stand-in for the real Stripe gateway, used on DEV and in tests so the
/// whole money path (intent creation, webhook verification, idempotent fulfilment) runs without a live
/// vendor account. It mirrors the contract the real adapter must honour: a stable intent id per
/// idempotency key, and HMAC-SHA256 webhook-signature verification over the raw body. The production
/// Stripe adapter (using Stripe.net + the Key Vault signing secret) is a drop-in replacement.
/// </summary>
public sealed class FakePaymentGateway(IOptions<PaymentsOptions> options) : IPaymentGateway
{
    private readonly PaymentsOptions _options = options.Value;

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Deterministic in the idempotency key: a retried create yields the same intent (no double charge).
        var id = $"pi_fake_{Hash(idempotencyKey)}";
        var clientSecret = $"{id}_secret_{Hash(idempotencyKey + ":secret")}";
        return Task.FromResult(new PaymentIntentResult(id, clientSecret));
    }

    public PaymentWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !SignatureValid(payload, signatureHeader))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventId = root.GetProperty("id").GetString();
            var type = root.GetProperty("type").GetString();
            var intentId = root.GetProperty("data").GetProperty("object").GetProperty("id").GetString();

            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(intentId))
            {
                return null;
            }

            return new PaymentWebhookEvent(eventId, type, intentId);
        }
        catch (JsonException)
        {
            return null; // malformed body → treat as an invalid event (400)
        }
    }

    /// <summary>The signature the caller must send: lowercase hex HMAC-SHA256 of the raw body under the
    /// shared secret. Exposed so tests (and the SPA's mock) can sign payloads the same way.</summary>
    public static string Sign(string payload, string secret)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    private bool SignatureValid(string payload, string signatureHeader)
    {
        var expected = Sign(payload, _options.WebhookSecret);
        // Constant-time compare so a timing side-channel can't be used to forge a signature.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];
}
