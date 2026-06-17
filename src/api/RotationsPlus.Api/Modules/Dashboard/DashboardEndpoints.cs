using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;

namespace RotationsPlus.Api.Modules.Dashboard;

/// <summary>
/// GET /api/dashboard — the admin hub. Aggregates domain totals, the rotation pipeline grouped by
/// status, and the next few upcoming rotation starts. AdminOnly (it surfaces rotation + student data).
/// "Today" comes from <see cref="TimeProvider"/> so the upcoming window is testable.
/// </summary>
public static class DashboardEndpoints
{
    private const int UpcomingCount = 8;

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dashboard", async (RotationsDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            // "Today" in UTC — fine for this internal hub; revisit if the upcoming window must honour
            // an operator's local zone near a UTC-midnight boundary.
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

            var students = await db.Students.CountAsync(cancellationToken);
            var programs = await db.Programs.CountAsync(cancellationToken);
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

            var response = new DashboardResponse(
                students, programs, preceptors, specialties,
                byStatus.Sum(s => s.Count),
                byStatus,
                upcoming);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("GetDashboard")
        .WithTags("Dashboard");

        return routes;
    }
}
