using FluentAssertions;
using RotationsPlus.Api.Modules.Payments;

namespace RotationsPlus.Api.Tests;

/// <summary>
/// Unit-tests the pricing rules (money risk area): total = retail × weeks; non-open programs take a
/// 10% deposit with the remainder outstanding; open programs are charged in full; and rounding stays
/// to the cent with deposit + outstanding always reconciling to the total.
/// </summary>
public class PricingServiceTests
{
    [Fact]
    public void Non_open_program_takes_a_ten_percent_deposit()
    {
        var quote = PricingService.Quote(retailAmountPerWeek: 1000m, weeks: 4, isOpen: false);

        quote.TotalAmount.Should().Be(4000m);
        quote.DepositAmount.Should().Be(400m);          // 10%
        quote.OutstandingAmount.Should().Be(3600m);     // remainder billed later
        quote.DepositPercent.Should().Be(10m);
    }

    [Fact]
    public void Open_program_is_charged_in_full_with_nothing_outstanding()
    {
        var quote = PricingService.Quote(retailAmountPerWeek: 1000m, weeks: 4, isOpen: true);

        quote.TotalAmount.Should().Be(4000m);
        quote.DepositAmount.Should().Be(4000m);          // 100%
        quote.OutstandingAmount.Should().Be(0m);
        quote.DepositPercent.Should().Be(100m);
    }

    [Theory]
    [InlineData(1500, 4, false)]
    [InlineData(1800, 1, false)]
    [InlineData(999.99, 3, false)]
    [InlineData(1234.56, 7, false)]
    [InlineData(1000, 4, true)]
    [InlineData(0, 4, false)]
    // Awkward sub-cent cases where the 10% deposit doesn't land on a clean cent — the outstanding must
    // still absorb the residue so the parts reconcile to the total exactly.
    [InlineData(0.05, 1, false)]          // 10% = 0.005 → rounds UP to 0.01 (away from zero)
    [InlineData(0.01, 1, false)]          // 10% = 0.001 → rounds to 0.00, outstanding carries the cent
    [InlineData(33.33, 1, false)]         // 10% = 3.333 → 3.33
    [InlineData(99999999.99, 520, false)] // largest possible total — proves no decimal overflow
    public void Deposit_plus_outstanding_always_reconciles_to_the_total(decimal perWeek, int weeks, bool isOpen)
    {
        var quote = PricingService.Quote(perWeek, weeks, isOpen);

        (quote.DepositAmount + quote.OutstandingAmount).Should().Be(quote.TotalAmount);
    }

    [Fact]
    public void Rounds_a_half_cent_deposit_up_away_from_zero()
    {
        // 0.05 × 1 = 0.05 total; 10% = 0.005, which must round UP to 0.01 (not banker's-round to 0.00),
        // leaving 0.04 outstanding. This pins the rounding DIRECTION, not just the reconciliation.
        var quote = PricingService.Quote(retailAmountPerWeek: 0.05m, weeks: 1, isOpen: false);

        quote.TotalAmount.Should().Be(0.05m);
        quote.DepositAmount.Should().Be(0.01m);
        quote.OutstandingAmount.Should().Be(0.04m);
    }

    [Fact]
    public void The_largest_possible_total_does_not_overflow()
    {
        // numeric(10,2) max retail × the 520-week cap — the biggest total the endpoint can request.
        var quote = PricingService.Quote(retailAmountPerWeek: 99999999.99m, weeks: 520, isOpen: false);

        quote.TotalAmount.Should().Be(51999999994.80m);
        (quote.DepositAmount + quote.OutstandingAmount).Should().Be(quote.TotalAmount);
    }

    [Fact]
    public void Rounds_the_deposit_to_the_cent_away_from_zero()
    {
        // 999.99 × 3 = 2999.97 total; 10% = 299.997 → rounds to 300.00. Outstanding is the exact remainder.
        var quote = PricingService.Quote(retailAmountPerWeek: 999.99m, weeks: 3, isOpen: false);

        quote.TotalAmount.Should().Be(2999.97m);
        quote.DepositAmount.Should().Be(300.00m);
        quote.OutstandingAmount.Should().Be(2699.97m);
        // No fractional cents leak into the persisted/charged amounts.
        decimal.Round(quote.DepositAmount, 2).Should().Be(quote.DepositAmount);
        decimal.Round(quote.OutstandingAmount, 2).Should().Be(quote.OutstandingAmount);
    }

    [Fact]
    public void A_zero_priced_program_quotes_zero_everywhere()
    {
        var quote = PricingService.Quote(retailAmountPerWeek: 0m, weeks: 4, isOpen: false);

        quote.TotalAmount.Should().Be(0m);
        quote.DepositAmount.Should().Be(0m);
        quote.OutstandingAmount.Should().Be(0m);
    }
}
