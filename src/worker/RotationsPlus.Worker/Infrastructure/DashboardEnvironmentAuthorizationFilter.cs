using Hangfire.Dashboard;

namespace RotationsPlus.Worker.Infrastructure;

/// <summary>
/// P1 dashboard gate: allow only when the flag is set (Development). In PREPROD/PROD the
/// dashboard is locked until staff Entra auth is wired onto the worker. See Plan_Architecture.md §3.4.
/// </summary>
public sealed class DashboardEnvironmentAuthorizationFilter(bool allow) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => allow;
}
