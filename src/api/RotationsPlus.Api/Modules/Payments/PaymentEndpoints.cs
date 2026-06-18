using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Payments endpoints. This first slice exposes the server-computed price quote for a program +
/// week count (the catalog/cart display calls it so the deposit math has one tested source of truth —
/// <see cref="PricingService"/>). Open to any marketplace viewer (staff + signed-in customers), since
/// students need a quote to decide on a booking. Persisting payments and the Stripe PaymentIntent +
/// webhook fulfillment land in later slices.
/// </summary>
public static class PaymentEndpoints
{
    // Upper bound on weeks for a quote — generous, matches the program MaxWeeksCap and guards against
    // absurd input that would multiply into an overflowing total.
    private const int MaxWeeks = 520;

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/programs")
            .RequireAuthorization(AuthorizationPolicies.MarketplaceViewer)
            .WithTags("Payments");

        group.MapGet("/{id:guid}/quote", async (
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

        return routes;
    }
}
