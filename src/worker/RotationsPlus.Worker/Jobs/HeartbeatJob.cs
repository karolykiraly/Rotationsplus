namespace RotationsPlus.Worker.Jobs;

/// <summary>
/// P1 no-op recurring job — proves the Hangfire server is scheduling and running work on DEV.
/// Replaced/joined by the real cron-replacement jobs (RotationStateMachineJob, etc.) per Plan_Architecture.md §3.4.
/// Time via TimeProvider (no DateTime.UtcNow) so scheduled logic stays testable — CLAUDE.md §4.
/// </summary>
public sealed class HeartbeatJob(ILogger<HeartbeatJob> logger)
{
    public Task RunAsync()
    {
        logger.LogInformation("rplus-worker heartbeat at {TimestampUtc:o}", TimeProvider.System.GetUtcNow());
        return Task.CompletedTask;
    }
}
