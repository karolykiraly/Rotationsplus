using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Dashboard;

/// <summary>
/// GET /api/dashboard — the admin hub. Aggregates domain totals, the program catalog by delivery
/// type, the rotation pipeline by status, the next upcoming rotation starts, and the day's
/// "LiveScore" movement (what was created today + today's rotation cycle). AdminOnly (it surfaces
/// rotation + student data). "Today" is the current <em>business</em> day (US/Pacific, matching the
/// legacy dashboard) and is driven by <see cref="TimeProvider"/> so the window is testable.
/// </summary>
public static class DashboardEndpoints
{
    private const int UpcomingCount = 8;

    /// <summary>Rotation statuses that count as a live rotation for the "today's cycle" breakdown
    /// (terminal/exception states — Cancelled/Refunded/Abandoned/Rejected/Completed — are excluded;
    /// Cancelled is surfaced via its own "cancelled today" count).</summary>
    private static readonly RotationStatus[] ActiveLifecycle =
        [RotationStatus.NotStarted, RotationStatus.Active, RotationStatus.ToBeEvaluated];

    /// <summary>The business time zone for the "today" window. The legacy dashboard used US/Pacific.
    /// Resolved cross-platform (IANA on Linux, Windows id as a fallback), then UTC as a last resort
    /// so a minimal container without the zone never fails the request.</summary>
    private static readonly TimeZoneInfo BusinessZone = ResolveBusinessZone();

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dashboard", async (RotationsDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            // "Now" in the business zone → the calendar day and the start-of-day instant (in UTC) that
            // bounds "created today". Date-only comparisons use the business calendar day.
            var nowBusiness = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessZone);
            var today = DateOnly.FromDateTime(nowBusiness.DateTime);
            // Resolve the offset for *this* date's midnight (not the current instant's offset), so the
            // window is correct on DST-transition days. Local midnight is always a valid instant here
            // (the spring-forward gap is 02:00–03:00), so no skipped-time ambiguity.
            var startOfDayUtc = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(today.ToDateTime(TimeOnly.MinValue), BusinessZone));

            // ---- Totals (LiveScore) ----
            var programsByType = await db.Programs
                .GroupBy(p => p.ProgramType)
                .Select(g => new ProgramTypeCount(g.Key, g.Count()))
                .ToListAsync(cancellationToken);
            var programs = programsByType.Sum(p => p.Count);

            var students = await db.Students.CountAsync(cancellationToken);
            var preceptors = await db.Preceptors.CountAsync(cancellationToken);
            var specialties = await db.Specialties.CountAsync(cancellationToken);

            var byStatus = await db.Rotations
                .GroupBy(r => r.Status)
                .Select(g => new RotationStatusCount(g.Key, g.Count()))
                .ToListAsync(cancellationToken);

            var upcoming = await db.Rotations
                .Where(r => r.StartDate >= today)
                .OrderBy(r => r.StartDate)
                .Take(UpcomingCount)
                .Select(r => new UpcomingRotation(
                    r.Id, r.StudentName, r.Program.Specialty.Name, r.StartDate, r.Status))
                .ToListAsync(cancellationToken);

            // ---- Today's LiveScore (movement within the current business day) ----
            var newProgramsByType = await db.Programs
                .Where(p => p.CreatedAtUtc >= startOfDayUtc)
                .GroupBy(p => p.ProgramType)
                .Select(g => new ProgramTypeCount(g.Key, g.Count()))
                .ToListAsync(cancellationToken);
            var newPrograms = newProgramsByType.Sum(p => p.Count);

            var newStudents = await db.Students.CountAsync(s => s.CreatedAtUtc >= startOfDayUtc, cancellationToken);
            var newPreceptors = await db.Preceptors.CountAsync(p => p.CreatedAtUtc >= startOfDayUtc, cancellationToken);

            // The today's-rotation-cycle buckets are mutually disjoint so they sum cleanly to the
            // headline number: a same-day rotation counts only as "starting"; "completing" excludes
            // it (StartDate < today); "in progress" is strictly mid-flight. Computed in ONE pass over
            // the rotations table (four filtered counts) instead of four separate COUNT round-trips —
            // EF translates Count(predicate) in an aggregate projection to COUNT(*) FILTER (WHERE ...).
            // GroupBy over a constant yields a single row (null only when the table is empty → all 0).
            var cycle = await db.Rotations
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Starting = g.Count(r => ActiveLifecycle.Contains(r.Status) && r.StartDate == today),
                    Completing = g.Count(r => ActiveLifecycle.Contains(r.Status) && r.EndDate == today && r.StartDate < today),
                    InProgress = g.Count(r => ActiveLifecycle.Contains(r.Status) && r.StartDate < today && r.EndDate > today),
                    Cancelled = g.Count(r => r.Status == RotationStatus.Cancelled && r.ModifiedAtUtc >= startOfDayUtc),
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Issues are a future subsystem (no Issue entity yet) — reported as 0 until it exists.
            var today_ = new TodayMetrics(
                newPrograms, newProgramsByType, newStudents, newPreceptors,
                IssuesReported: 0,
                cycle?.Starting ?? 0, cycle?.InProgress ?? 0, cycle?.Completing ?? 0, cycle?.Cancelled ?? 0);

            var response = new DashboardResponse(
                students, programs, preceptors, specialties,
                byStatus.Sum(s => s.Count),
                programsByType,
                byStatus,
                upcoming,
                today_);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("GetDashboard")
        .WithTags("Dashboard");

        return routes;
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
