using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Crm;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Worker.Jobs;

/// <summary>
/// Recurring safety net for campaign sends. Two things can strand a campaign short of a terminal state:
/// <list type="bullet">
/// <item>a <see cref="CampaignStatus.Queued"/> campaign whose enqueue was lost (the API stamped Queued but
/// the Hangfire job never arrived — e.g. a crash between the DB commit and the enqueue);</item>
/// <item>a <see cref="CampaignStatus.Sending"/> campaign whose Worker died mid-fan-out.</item>
/// </list>
/// This sweep re-dispatches the first kind and resets+re-dispatches the second. Because
/// <see cref="SendCampaignJob"/> claims atomically and only processes still-Pending recipients, every
/// re-dispatch is idempotent — at worst it resumes work, never duplicates a completed send.
///
/// <para>The Sending timeout is deliberately generous (far longer than any real fan-out) so the sweep
/// can't reset a campaign that's merely slow out from under a live run; a reset is guarded on the same
/// stale <c>SendStartedAtUtc</c> it observed, so a send that finishes first is left alone.</para>
/// </summary>
public sealed class CampaignSweepJob(
    RotationsDbContext db,
    ICampaignDispatcher dispatcher,
    TimeProvider timeProvider,
    ILogger<CampaignSweepJob> logger)
{
    /// <summary>A campaign still Queued this long after it was queued had its dispatch lost — re-enqueue it.
    /// Comfortably longer than the Hangfire pickup latency so a normally-queued campaign isn't double-sent.</summary>
    public static readonly TimeSpan QueuedStuckAfter = TimeSpan.FromMinutes(3);

    /// <summary>A campaign Sending this long after it was claimed crashed mid-send — reset and resume. Far
    /// beyond any real fan-out duration, so the sweep never races a live (merely slow) send.</summary>
    public static readonly TimeSpan SendingStuckAfter = TimeSpan.FromMinutes(15);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var queuedCutoff = now - QueuedStuckAfter;
        var sendingCutoff = now - SendingStuckAfter;

        var resumed = await RecoverStuckSendingAsync(now, sendingCutoff, cancellationToken);
        var redispatched = await RedispatchStuckQueuedAsync(queuedCutoff, cancellationToken);

        if (resumed > 0 || redispatched > 0)
        {
            logger.LogWarning(
                "CampaignSweepJob: recovered {Resumed} stuck-sending and re-dispatched {Redispatched} stuck-queued campaigns.",
                resumed, redispatched);
        }
    }

    /// <summary>Resets campaigns stuck in Sending back to Queued (guarded on the stale claim time) and
    /// re-dispatches each one actually reset.</summary>
    private async Task<int> RecoverStuckSendingAsync(DateTimeOffset now, DateTimeOffset sendingCutoff, CancellationToken cancellationToken)
    {
        var stuckIds = await db.EmailCampaigns
            .Where(c => c.Status == CampaignStatus.Sending
                && c.SendStartedAtUtc != null && c.SendStartedAtUtc < sendingCutoff)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var resumed = 0;
        foreach (var id in stuckIds)
        {
            // Guarded reset: only a campaign still Sending with the same stale claim time is flipped, so a
            // send that completed (or was re-claimed) between the query and here is left untouched. Stamp
            // ModifiedAtUtc to "now" too (the Worker has no audit interceptor, and this is a direct UPDATE)
            // so the just-requeued row isn't immediately re-matched by the Queued sweep in this same pass.
            var reset = await db.EmailCampaigns
                .Where(c => c.Id == id && c.Status == CampaignStatus.Sending
                    && c.SendStartedAtUtc != null && c.SendStartedAtUtc < sendingCutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, CampaignStatus.Queued)
                    .SetProperty(c => c.SendStartedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(c => c.ModifiedAtUtc, now), cancellationToken);

            if (reset == 1)
            {
                dispatcher.Dispatch(id);
                resumed++;
            }
        }

        return resumed;
    }

    /// <summary>Re-dispatches campaigns that have sat in Queued past the cutoff (their original enqueue was
    /// lost). No state change is needed — the send job will claim Queued → Sending; a duplicate enqueue is
    /// harmless because only one claim wins.</summary>
    private async Task<int> RedispatchStuckQueuedAsync(DateTimeOffset queuedCutoff, CancellationToken cancellationToken)
    {
        var stuckIds = await db.EmailCampaigns
            .Where(c => c.Status == CampaignStatus.Queued
                && (c.ModifiedAtUtc ?? c.CreatedAtUtc) < queuedCutoff)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in stuckIds)
        {
            dispatcher.Dispatch(id);
        }

        return stuckIds.Count;
    }
}
