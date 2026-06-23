using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Contracts.Dashboard;

/// <summary>The admin dashboard "Revenue" tab. All figures are platform revenue (deposits captured),
/// not preceptor honorarium (a payout/cost, excluded). Money is held as decimal to the cent in a single
/// <see cref="Currency"/>. A refund flips the original payment's status in place, so <see cref="Collected"/>
/// (sum over currently-Succeeded payments) already nets out refunds — <see cref="Refunded"/> is reported
/// separately for visibility, not subtracted again. Scoped to live (non-deleted) rotations.</summary>
public sealed record DashboardRevenueResponse(
    string Currency,
    decimal Collected,
    decimal Refunded,
    decimal OutstandingReceivable,
    decimal CollectedThisMonth,
    IReadOnlyList<RevenueByType> ByProgramType,
    IReadOnlyList<RevenueByMonth> MonthlyTrend);

/// <summary>Collected revenue for one program delivery type (sums to <see cref="DashboardRevenueResponse.Collected"/>).</summary>
public sealed record RevenueByType(ProgramType Type, decimal Amount);

/// <summary>Collected revenue captured within one business month (US/Pacific), for the trend series.</summary>
public sealed record RevenueByMonth(int Year, int Month, decimal Amount);
