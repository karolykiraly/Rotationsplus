using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// The payment-provider webhook — the ONLY place a payment is fulfilled (Plan_Student §52: fulfilment
/// is webhook-driven, not browser-driven, which closes the legacy silent-loss gap). The endpoint is
/// anonymous; its authentication is the provider signature, verified by <see cref="IPaymentGateway"/>.
/// Delivery is at-least-once, so processing is idempotent two ways: a per-event ledger
/// (<see cref="ProcessedWebhookEvent"/>) skips re-delivered events, and the fulfilment itself only acts
/// on a still-<see cref="PaymentStatus.Pending"/> payment.
/// </summary>
public static class PaymentWebhookEndpoints
{
    public static IEndpointRouteBuilder MapPaymentWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/webhooks/stripe", async (
            HttpRequest request, RotationsDbContext db, IPaymentGateway gateway, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            // This endpoint is anonymous, so cap the body the provider can post (a real webhook is a few
            // KB). Without this, an unauthenticated caller could stream an unbounded body and exhaust
            // memory. Exceeding the cap makes ReadToEndAsync throw BadHttpRequestException → 400. This is
            // enforced by Kestrel in the deployed app; under the in-memory TestServer the feature is
            // absent/read-only and the guard is a no-op (the IsReadOnly check skips it).
            var maxBodySize = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxBodySize is { IsReadOnly: false })
            {
                maxBodySize.MaxRequestBodySize = 64 * 1024;
            }

            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            var signature = request.Headers["Stripe-Signature"].ToString();

            var webhookEvent = gateway.ParseWebhookEvent(payload, signature);
            if (webhookEvent is null)
            {
                // Missing/invalid signature or unparseable body — reject so a forged call can't reach fulfilment.
                return Results.BadRequest();
            }

            // Already processed (re-delivery) → acknowledge without acting again.
            if (await db.ProcessedWebhookEvents.AnyAsync(e => e.Id == webhookEvent.EventId, cancellationToken))
            {
                return Results.Ok();
            }

            await PaymentFulfillment.FulfillAsync(db, webhookEvent, cancellationToken);

            db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
            {
                Id = webhookEvent.EventId,
                ReceivedAtUtc = clock.GetUtcNow(),
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // A concurrent re-delivery recorded the same event id first (PK clash on the ledger) — the
                // other call did the fulfilment in its own transaction; treat this one as a no-op success.
                // Narrowed to the unique-violation SQLSTATE so any other write failure still re-throws (500)
                // and the provider retries.
                if (await EventAlreadyRecordedAsync(db, webhookEvent.EventId, cancellationToken))
                {
                    return Results.Ok();
                }
                throw;
            }

            return Results.Ok();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.Webhook)
        .WithName("StripeWebhook")
        .WithTags("Payments");

        return routes;
    }

    private static Task<bool> EventAlreadyRecordedAsync(RotationsDbContext db, string eventId, CancellationToken cancellationToken) =>
        db.ProcessedWebhookEvents.AsNoTracking().AnyAsync(e => e.Id == eventId, cancellationToken);
}
