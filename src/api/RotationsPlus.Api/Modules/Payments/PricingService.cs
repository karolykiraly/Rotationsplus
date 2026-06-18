namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// The single source of truth for rotation pricing. Pure (no I/O) and fully unit-tested because
/// pricing is a money risk area — the legacy system duplicated these rules across client and server.
///
/// Rules (Plan_Student.md §4):
/// <list type="bullet">
///   <item>Total = retail-per-week × weeks.</item>
///   <item>Open (instant-approval) programs are charged in full at checkout (100% deposit, nothing
///   outstanding).</item>
///   <item>Non-open programs take a <see cref="DepositRate"/> (10%) deposit; the remainder is billed
///   later as an outstanding payment.</item>
/// </list>
/// All amounts are rounded to the cent (away from zero) so the persisted/charged values are exact.
/// Consultation hourly pricing (charged by consultation hours rather than weeks) is a tracked
/// follow-up and is not modelled here.
/// </summary>
public static class PricingService
{
    /// <summary>Deposit fraction taken at checkout for non-open programs.</summary>
    public const decimal DepositRate = 0.10m;

    /// <summary>The only currency the platform transacts in today.</summary>
    public const string Currency = "USD";

    /// <summary>
    /// Computes a quote for booking a program priced at <paramref name="retailAmountPerWeek"/> for
    /// <paramref name="weeks"/> weeks. <paramref name="isOpen"/> selects the full-payment (open) vs.
    /// deposit (non-open) rule. Caller is responsible for validating the inputs (weeks ≥ 1, etc.);
    /// this method assumes already-valid, non-negative inputs.
    /// </summary>
    public static PricingQuote Quote(decimal retailAmountPerWeek, int weeks, bool isOpen)
    {
        var total = Round(retailAmountPerWeek * weeks);
        // Open programs are paid in full; non-open take a 10% deposit. The deposit is rounded to the
        // cent and the outstanding is the exact remainder, so deposit + outstanding == total always.
        var deposit = isOpen ? total : Round(total * DepositRate);
        var outstanding = total - deposit;
        var depositPercent = isOpen ? 100m : DepositRate * 100m;
        return new PricingQuote(total, deposit, outstanding, depositPercent);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>The computed money figures for a booking. <see cref="DepositAmount"/> +
/// <see cref="OutstandingAmount"/> always equals <see cref="TotalAmount"/>.</summary>
public readonly record struct PricingQuote(
    decimal TotalAmount,
    decimal DepositAmount,
    decimal OutstandingAmount,
    decimal DepositPercent);
