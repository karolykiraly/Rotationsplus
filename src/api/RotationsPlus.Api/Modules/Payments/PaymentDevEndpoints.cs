using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// DEV-ONLY payment simulation — the local analog of the Stripe CLI's <c>stripe trigger</c>. On DEV the
/// gateway is the deterministic <see cref="FakePaymentGateway"/> (no real Stripe.js, so the browser can't
/// confirm a card and no real webhook ever fires), yet we still want to exercise the full deposit→approval
/// round-trip from the SPA. This endpoint lets the signed-in student push their own pending deposit to a
/// terminal outcome, running it through the SAME <see cref="PaymentFulfillment"/> path the real webhook
/// uses. It is mapped ONLY on non-Production environments (DEV's "Development" and the integration-test
/// host's "Testing" — see Program.cs), so it does not exist on PREPROD/PROD (both run as "Production"),
/// where a real Stripe event drives fulfilment.
/// </summary>
public static class PaymentDevEndpoints
{
    public static IEndpointRouteBuilder MapPaymentDevEndpoints(this IEndpointRouteBuilder routes)
    {
        // CustomerOnly + the same oid→student→rotation ownership check as opening the deposit, so even on
        // DEV a customer can only simulate against their own payment — it is not an open fulfilment hole.
        routes.MapPost("/api/dev/payments/{paymentId:guid}/simulate", async (
            Guid paymentId, PaymentSimulationRequest request, ICurrentUser user, RotationsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.NotFound();
            }

            var type = request.Outcome?.ToLowerInvariant() switch
            {
                "succeeded" => PaymentWebhookEventTypes.PaymentSucceeded,
                "failed" => PaymentWebhookEventTypes.PaymentFailed,
                _ => null,
            };
            if (type is null)
            {
                return Results.BadRequest("outcome must be 'succeeded' or 'failed'.");
            }

            var payment = await db.Payments
                .Where(p => p.Id == paymentId
                    && db.Rotations.Any(r => r.Id == p.RotationId
                        && db.Students.Any(s => s.Id == r.StudentId && s.StudentOid == oid)))
                .FirstOrDefaultAsync(cancellationToken);

            // Non-owner or missing payment → 404 (indistinguishable, same as the deposit endpoint).
            if (payment is null || string.IsNullOrEmpty(payment.ProviderPaymentIntentId))
            {
                return Results.NotFound();
            }

            // Drive the shared fulfilment with a synthetic event keyed to this payment's intent id — exactly
            // what a real provider webhook would carry. FulfillAsync re-reads the same tracked payment and
            // only acts on a Pending one, so a repeat simulate is a harmless no-op.
            var simulatedEvent = new PaymentWebhookEvent($"evt_dev_{Guid.NewGuid():N}", type, payment.ProviderPaymentIntentId);
            await PaymentFulfillment.FulfillAsync(db, simulatedEvent, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new PaymentSimulationResponse(payment.Id, payment.Status));
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("SimulatePayment")
        .WithTags("Payments");

        return routes;
    }
}

/// <summary>DEV-only: which terminal outcome to simulate for a pending deposit ("succeeded"|"failed").</summary>
public sealed record PaymentSimulationRequest(string Outcome);

/// <summary>DEV-only: the payment's resulting status after the simulated outcome.</summary>
public sealed record PaymentSimulationResponse(Guid PaymentId, PaymentStatus Status);
