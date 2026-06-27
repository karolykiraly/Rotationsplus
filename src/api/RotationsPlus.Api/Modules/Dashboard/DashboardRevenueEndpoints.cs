using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Payments;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Dashboard;

/// <summary>
/// GET /api/dashboard/revenue — the admin hub's "Revenue" tab. Platform revenue = deposits captured
/// (Payment.Amount on Succeeded payments); preceptor honorarium is a payout, not revenue, and is
/// excluded. A refund flips the payment's status to Refunded in place, so the Succeeded sum already
/// nets refunds out — Refunded is surfaced separately, never subtracted twice. Everything is scoped to
/// live (non-deleted) rotations so the per-type breakdown sums to the headline and no payment on a
/// soft-deleted booking leaks in via a NULL navigation. AdminOnly. The "this month"/trend windows use
/// the business calendar (US/Pacific), driven by <see cref="TimeProvider"/> so they're testable.
/// </summary>
public static class DashboardRevenueEndpoints
{
    private const int TrendMonths = 6;

    private static readonly TimeZoneInfo BusinessZone = ResolveBusinessZone();

    public static IEndpointRouteBuilder MapDashboardRevenueEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dashboard/revenue", async (RotationsDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            // Only payments whose rotation is still live count — a payment on a soft-deleted booking
            // would otherwise project a NULL program type and break the "by-type sums to collected"
            // invariant (mirrors the documents-todo soft-delete guard).
            var live = db.Payments.Where(p => !p.Rotation.IsDeleted);
            var succeeded = live.Where(p => p.Status == PaymentStatus.Succeeded);

            var collected = await succeeded.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            var refunded = await live.Where(p => p.Status == PaymentStatus.Refunded)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            var outstanding = await succeeded.SumAsync(p => (decimal?)p.OutstandingAmount, cancellationToken) ?? 0m;

            // Business-month boundaries: start of the current month, and the start of the trend window
            // (TrendMonths-1 months earlier), each resolved to the UTC instant for the row filter.
            var nowBusiness = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessZone);
            var monthStartUtc = StartOfBusinessMonthUtc(nowBusiness.Year, nowBusiness.Month);
            var collectedThisMonth = await succeeded.Where(p => p.CreatedAtUtc >= monthStartUtc)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var byProgramType = await succeeded
                .GroupBy(p => p.Rotation.Program.ProgramType)
                .Select(g => new RevenueByType(g.Key, g.Sum(p => p.Amount)))
                .ToListAsync(cancellationToken);

            // Trend: pull the (capture instant, amount) for succeeded payments in the window, then bucket
            // by business month in memory (timezone-correct month bucketing isn't worth a raw SQL cast,
            // and the window is bounded to TrendMonths of deposits).
            var firstMonth = AddMonths(nowBusiness.Year, nowBusiness.Month, -(TrendMonths - 1));
            var windowStartUtc = StartOfBusinessMonthUtc(firstMonth.Year, firstMonth.Month);
            var captures = await succeeded.Where(p => p.CreatedAtUtc >= windowStartUtc)
                .Select(p => new { p.CreatedAtUtc, p.Amount })
                .ToListAsync(cancellationToken);

            var buckets = new Dictionary<(int Year, int Month), decimal>();
            for (var i = 0; i < TrendMonths; i++)
            {
                var m = AddMonths(firstMonth.Year, firstMonth.Month, i);
                buckets[m] = 0m;
            }
            foreach (var c in captures)
            {
                var b = TimeZoneInfo.ConvertTime(c.CreatedAtUtc, BusinessZone);
                if (buckets.ContainsKey((b.Year, b.Month)))
                {
                    buckets[(b.Year, b.Month)] += c.Amount;
                }
            }
            var monthlyTrend = buckets
                .OrderBy(kv => kv.Key.Year).ThenBy(kv => kv.Key.Month)
                .Select(kv => new RevenueByMonth(kv.Key.Year, kv.Key.Month, kv.Value))
                .ToList();

            var response = new DashboardRevenueResponse(
                PricingService.Currency, collected, refunded, outstanding, collectedThisMonth,
                byProgramType, monthlyTrend);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("GetDashboardRevenue")
        .WithTags("Dashboard");

        return routes;
    }

    private static (int Year, int Month) AddMonths(int year, int month, int delta)
    {
        var zeroBased = (year * 12 + (month - 1)) + delta;
        return (zeroBased / 12, zeroBased % 12 + 1);
    }

    /// <summary>The UTC instant of midnight on the first of the given business month. Local midnight on
    /// the 1st is always a valid, unambiguous instant (no DST gap there), so the conversion is safe.</summary>
    private static DateTimeOffset StartOfBusinessMonthUtc(int year, int month)
    {
        var localMidnight = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, BusinessZone));
    }

    private static TimeZoneInfo ResolveBusinessZone()
    {
        foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Try the next id form, then fall through to UTC.
            }
        }

        return TimeZoneInfo.Utc;
    }
}
