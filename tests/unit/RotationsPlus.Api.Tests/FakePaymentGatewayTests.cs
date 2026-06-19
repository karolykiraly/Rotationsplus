using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RotationsPlus.Api.Modules.Payments;

namespace RotationsPlus.Api.Tests;

/// <summary>
/// Unit-tests the fake payment gateway's two security-relevant behaviours: a deterministic intent per
/// idempotency key (so a retried create can't double-charge) and HMAC webhook-signature verification
/// (the webhook's only authentication). The real Stripe adapter must honour the same contract.
/// </summary>
public class FakePaymentGatewayTests
{
    private const string Secret = "test-secret";

    private static FakePaymentGateway Gateway() =>
        new(Options.Create(new PaymentsOptions { WebhookSecret = Secret }));

    private static string WebhookPayload(string id, string type, string intentId) =>
        JsonSerializer.Serialize(new { id, type, data = new { @object = new { id = intentId } } });

    private static string SignedPayload(string id, string type, string intentId, out string payload)
    {
        payload = WebhookPayload(id, type, intentId);
        return FakePaymentGateway.Sign(payload, Secret);
    }

    [Fact]
    public async Task Same_idempotency_key_yields_the_same_intent()
    {
        var gateway = Gateway();
        var meta = new Dictionary<string, string>();

        var first = await gateway.CreatePaymentIntentAsync(40000, "USD", "key-1", meta, default);
        var second = await gateway.CreatePaymentIntentAsync(40000, "USD", "key-1", meta, default);
        var different = await gateway.CreatePaymentIntentAsync(40000, "USD", "key-2", meta, default);

        second.PaymentIntentId.Should().Be(first.PaymentIntentId);
        second.ClientSecret.Should().Be(first.ClientSecret);
        different.PaymentIntentId.Should().NotBe(first.PaymentIntentId);
    }

    [Fact]
    public async Task Same_refund_key_yields_the_same_refund_id()
    {
        var gateway = Gateway();

        var first = await gateway.RefundAsync("pi_123", "refund-1", default);
        var second = await gateway.RefundAsync("pi_123", "refund-1", default);
        var different = await gateway.RefundAsync("pi_123", "refund-2", default);

        first.RefundId.Should().StartWith("re_fake_");
        second.RefundId.Should().Be(first.RefundId);       // idempotent: no double refund
        different.RefundId.Should().NotBe(first.RefundId);
    }

    [Fact]
    public void Parses_a_correctly_signed_event()
    {
        var gateway = Gateway();
        var signature = SignedPayload("evt_1", PaymentWebhookEventTypes.PaymentSucceeded, "pi_123", out var payload);

        var result = gateway.ParseWebhookEvent(payload, signature);

        result.Should().NotBeNull();
        result!.EventId.Should().Be("evt_1");
        result.Type.Should().Be(PaymentWebhookEventTypes.PaymentSucceeded);
        result.PaymentIntentId.Should().Be("pi_123");
    }

    [Fact]
    public void Rejects_an_event_signed_with_the_wrong_secret()
    {
        var gateway = Gateway();
        var payload = """{"id":"evt_1","type":"payment_intent.succeeded","data":{"object":{"id":"pi_123"}}}""";
        var forged = FakePaymentGateway.Sign(payload, "wrong-secret");

        gateway.ParseWebhookEvent(payload, forged).Should().BeNull();
    }

    [Fact]
    public void Rejects_a_tampered_payload_under_a_valid_old_signature()
    {
        var gateway = Gateway();
        SignedPayload("evt_1", PaymentWebhookEventTypes.PaymentSucceeded, "pi_123", out var original);
        var signature = FakePaymentGateway.Sign(original, Secret);
        var tampered = original.Replace("pi_123", "pi_attacker");

        gateway.ParseWebhookEvent(tampered, signature).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-signature")]
    public void Rejects_missing_or_garbage_signatures(string? signature)
    {
        var gateway = Gateway();
        var payload = """{"id":"evt_1","type":"payment_intent.succeeded","data":{"object":{"id":"pi_123"}}}""";

        gateway.ParseWebhookEvent(payload, signature).Should().BeNull();
    }

    [Fact]
    public void Rejects_a_validly_signed_but_malformed_body()
    {
        var gateway = Gateway();
        var payload = "this is not json";
        var signature = FakePaymentGateway.Sign(payload, Secret);

        gateway.ParseWebhookEvent(payload, signature).Should().BeNull();
    }
}
