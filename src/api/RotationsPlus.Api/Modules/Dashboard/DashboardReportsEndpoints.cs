using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;

namespace RotationsPlus.Api.Modules.Dashboard;

/// <summary>
/// GET /api/dashboard/reports — the admin hub's "Reports" tab. Read-only operational analytics: the
/// booking-conversion funnel (registered students vs. those who have booked ≥1 rotation), a six-month
/// registration trend (students + preceptors per business month), and the busiest specialties by
/// rotation volume. All counts are over live (non-deleted) rows via the global query filters. AdminOnly.
/// The trend window uses the business calendar (US/Pacific), driven by <see cref="TimeProvider"/>.
/// </summary>
public static class DashboardReportsEndpoints
{
    private const int TrendMonths = 6;
    private const int TopSpecialtyCount = 8;

    public static IEndpointRouteBuilder MapDashboardReportsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dashboard/reports", async (RotationsDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var totalStudents = await db.Students.CountAsync(cancellationToken);
            var totalRotations = await db.Rotations.CountAsync(cancellationToken);

            // Distinct students who have booked at least one (live) rotation — the conversion numerator.
            var studentsWithBooking = await db.Rotations
                .Where(r => r.StudentId != null)
                .Select(r => r.StudentId)
                .Distinct()
                .CountAsync(cancellationToken);

            // Busiest specialties by rotation count. The Specialty nav is required, but a specialty can
            // be soft-deleted while a program still references it (specialty delete is unguarded), which
            // would surface a NULL group key under the query filter — exclude those so the contract's
            // non-null SpecialtyName holds. The GroupBy+Count runs in SQL (an anonymous projection, like
            // the revenue by-type query); the ordering/Take is done in memory — EF can't translate an
            // OrderBy over a *positional-record* projection, and specialty cardinality is tiny.
            var specialtyCounts = await db.Rotations
                .Where(r => r.Program.Specialty.Name != null)
                .GroupBy(r => r.Program.Specialty.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            var topSpecialties = specialtyCounts
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(TopSpecialtyCount)
                .Select(x => new RotationsBySpecialty(x.Name!, x.Count))
                .ToList();

            // Registration trend: the (created instant) of students + preceptors in the 6-month window,
            // bucketed by business month in memory (timezone-correct month bucketing isn't worth a raw
            // SQL cast, and the window is bounded).
            var nowBusiness = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessCalendar.Zone);
            var firstMonth = BusinessCalendar.AddMonths(nowBusiness.Year, nowBusiness.Month, -(TrendMonths - 1));
            var windowStartUtc = BusinessCalendar.StartOfMonthUtc(firstMonth.Year, firstMonth.Month);

            var studentDates = await db.Students.Where(s => s.CreatedAtUtc >= windowStartUtc)
                .Select(s => s.CreatedAtUtc).ToListAsync(cancellationToken);
            var preceptorDates = await db.Preceptors.Where(p => p.CreatedAtUtc >= windowStartUtc)
                .Select(p => p.CreatedAtUtc).ToListAsync(cancellationToken);

            var students = NewMonthBuckets(firstMonth);
            var preceptors = NewMonthBuckets(firstMonth);
            foreach (var d in studentDates) Increment(students, d);
            foreach (var d in preceptorDates) Increment(preceptors, d);

            var registrations = students.Keys
                .OrderBy(k => k.Year).ThenBy(k => k.Month)
                .Select(k => new RegistrationsByMonth(k.Year, k.Month, students[k], preceptors[k]))
                .ToList();

            var response = new DashboardReportsResponse(
                totalStudents, studentsWithBooking, totalRotations, registrations, topSpecialties);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("GetDashboardReports")
        .WithTags("Dashboard");

        return routes;
    }

    private static Dictionary<(int Year, int Month), int> NewMonthBuckets((int Year, int Month) firstMonth)
    {
        var buckets = new Dictionary<(int, int), int>();
        for (var i = 0; i < TrendMonths; i++)
        {
            var m = BusinessCalendar.AddMonths(firstMonth.Year, firstMonth.Month, i);
            buckets[m] = 0;
        }
        return buckets;
    }

    private static void Increment(Dictionary<(int Year, int Month), int> buckets, DateTimeOffset instant)
    {
        var b = TimeZoneInfo.ConvertTime(instant, BusinessCalendar.Zone);
        if (buckets.ContainsKey((b.Year, b.Month)))
        {
            buckets[(b.Year, b.Month)]++;
        }
    }
}
