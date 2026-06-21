using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Customer-facing payment endpoints. The price <c>quote</c> (open to any marketplace viewer) and the
/// deposit <c>payment-intent</c> (the signed-in student opening a deposit for their own rotation). The
/// money math comes from <see cref="PricingService"/>; the provider intent comes from
/// <see cref="IPaymentGateway"/> (a fake on DEV). Fulfilment is handled by the webhook, never here.
/// </summary>
public static class PaymentEndpoints
{
    // Upper bound on weeks for a quote — generous, matches the program MaxWeeksCap and guards against
    // absurd input that would multiply into an overflowing total.
    private const int MaxWeeks = 520;

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var programs = routes.MapGroup("/api/programs")
            .RequireAuthorization(AuthorizationPolicies.MarketplaceViewer)
            .WithTags("Payments");

        programs.MapGet("/{id:guid}/quote", async (
            Guid id, int? weeks, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var program = await db.Programs
                .Where(p => p.Id == id)
                .Select(p => new { p.RetailAmountPerWeek, p.MinWeeksPerRotation, p.IsOpen })
                .FirstOrDefaultAsync(cancellationToken);

            if (program is null)
            {
                return Results.NotFound();
            }

            if (weeks is not int w || w < 1 || w > MaxWeeks)
            {
                return Results.BadRequest($"weeks must be between 1 and {MaxWeeks}.");
            }

            if (w < program.MinWeeksPerRotation)
            {
                return Results.BadRequest($"This program requires at least {program.MinWeeksPerRotation} week(s).");
            }

            var quote = PricingService.Quote(program.RetailAmountPerWeek, w, program.IsOpen);
            return Results.Ok(new RotationQuoteResponse(
                id,
                w,
                PricingService.Currency,
                program.RetailAmountPerWeek,
                quote.TotalAmount,
                quote.DepositAmount,
                quote.OutstandingAmount,
                quote.DepositPercent,
                program.IsOpen));
        })
        .WithName("GetRotationQuote");

        // Opening a deposit is the signed-in student paying for their OWN rotation. CustomerOnly + an
        // ownership match (oid → directory student → rotation) so a customer can't open a payment for
        // someone else's booking; a non-owner is indistinguishable from a missing rotation (404).
        routes.MapPost("/api/rotations/{id:guid}/payment-intent", async (
            Guid id, ICurrentUser user, RotationsDbContext db, IPaymentGateway gateway, CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.NotFound();
            }

            var rotation = await db.Rotations
                .Where(r => r.Id == id && db.Students.Any(s => s.Id == r.StudentId && s.StudentOid == oid))
                .Select(r => new { r.Status, r.Program.RetailAmountPerWeek, r.Weeks, r.Program.IsOpen })
                .FirstOrDefaultAsync(cancellationToken);

            if (rotation is null)
            {
                return Results.NotFound();
            }

            // A deposit only opens a booking that is awaiting approval. Don't take money for a rotation
            // that's already approved/active or in a terminal state (cancelled/rejected/refunded/etc.) —
            // that would leave a paid-but-dead booking needing a manual refund.
            if (rotation.Status != RotationStatus.Pending)
            {
                return Results.Conflict("This rotation isn't awaiting a deposit.");
            }

            var quote = PricingService.Quote(rotation.RetailAmountPerWeek, rotation.Weeks, rotation.IsOpen);
            if (quote.DepositAmount <= 0)
            {
                // No money is due (e.g. a zero-priced program). Charging $0 isn't meaningful; the
                // free-booking confirmation path is a tracked follow-up.
                return Results.BadRequest("This rotation has no deposit due.");
            }

            // Don't double-charge: a succeeded deposit is final; a pending one is re-offered.
            var active = await ActiveDepositAsync(db, id, cancellationToken);
            if (active is { Status: PaymentStatus.Succeeded })
            {
                return Results.Conflict("This rotation's deposit has already been paid.");
            }
            if (active is { Status: PaymentStatus.Pending })
            {
                return Results.Ok(await ReofferAsync(db, gateway, active, cancellationToken));
            }

            var payment = new Payment
            {
                RotationId = id,
                Amount = quote.DepositAmount,
                TotalAmount = quote.TotalAmount,
                OutstandingAmount = quote.OutstandingAmount,
                Currency = PricingService.Currency,
                Status = PaymentStatus.Pending,
                IdempotencyKey = string.Empty, // set from the generated id below (unique per attempt)
            };
            // Key the gateway idempotency to THIS payment, not the rotation, so a retry after a failed
            // attempt opens a fresh intent instead of colliding on the prior key.
            payment.IdempotencyKey = $"deposit-{payment.Id}";
            var metadata = new Dictionary<string, string>
            {
                ["rotationId"] = id.ToString(),
                ["paymentId"] = payment.Id.ToString(),
            };

            var intent = await gateway.CreatePaymentIntentAsync(
                ToMinorUnits(payment.Amount), payment.Currency, payment.IdempotencyKey, metadata, cancellationToken);
            payment.ProviderPaymentIntentId = intent.PaymentIntentId;
            db.Payments.Add(payment);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (IsUniqueViolation(e))
            {
                // Lost a concurrent race for this rotation's single active-deposit slot. The other call's
                // payment is the live one; return its intent (our just-created provider intent is never
                // confirmed and harmlessly expires). If it already succeeded meanwhile, it's paid.
                var winner = await ActiveDepositAsync(db, id, cancellationToken);
                if (winner is { Status: PaymentStatus.Succeeded })
                {
                    return Results.Conflict("This rotation's deposit has already been paid.");
                }
                if (winner is { Status: PaymentStatus.Pending })
                {
                    return Results.Ok(await ReofferAsync(db, gateway, winner, cancellationToken));
                }
                throw;
            }

            return Results.Created($"/api/payments/{payment.Id}", ToResponse(payment, intent.ClientSecret));
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .RequireRateLimiting(RateLimitPolicies.Payments)
        .WithName("CreateRotationPaymentIntent")
        .WithTags("Payments");

        return routes;
    }

    /// <summary>Converts a money amount (already cents-exact) to the provider's integer minor units.</summary>
    internal static long ToMinorUnits(decimal amount) => (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>The rotation's single active deposit (Pending or Succeeded), if any.</summary>
    private static Task<Payment?> ActiveDepositAsync(RotationsDbContext db, Guid rotationId, CancellationToken cancellationToken) =>
        db.Payments
            .Where(p => p.RotationId == rotationId
                && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Succeeded))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Re-offers an existing pending deposit: the gateway returns the same intent for the same
    /// idempotency key, so the client just gets its secret again (no second charge). The create path
    /// always records the intent id before saving, so a committed Pending row already has it and the
    /// back-fill below is a no-op today; it stays as a defensive guard for any future path that persists
    /// a payment before its gateway call returns.</summary>
    private static async Task<PaymentIntentResponse> ReofferAsync(
        RotationsDbContext db, IPaymentGateway gateway, Payment payment, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            ["rotationId"] = payment.RotationId.ToString(),
            ["paymentId"] = payment.Id.ToString(),
        };
        var intent = await gateway.CreatePaymentIntentAsync(
            ToMinorUnits(payment.Amount), payment.Currency, payment.IdempotencyKey, metadata, cancellationToken);

        if (payment.ProviderPaymentIntentId != intent.PaymentIntentId)
        {
            payment.ProviderPaymentIntentId = intent.PaymentIntentId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(payment, intent.ClientSecret);
    }

    /// <summary>True when the exception is a Postgres unique-violation (SQLSTATE 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static PaymentIntentResponse ToResponse(Payment payment, string clientSecret) =>
        new(payment.Id, clientSecret, payment.Amount, payment.TotalAmount, payment.OutstandingAmount, payment.Currency, payment.Status);
}
