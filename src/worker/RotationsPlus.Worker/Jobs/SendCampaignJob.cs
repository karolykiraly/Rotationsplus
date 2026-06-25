using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Crm;
using RotationsPlus.Common.Email;
using RotationsPlus.Common.Jobs;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Worker.Jobs;

/// <summary>
/// Sends a queued email campaign: claims it, materialises a per-recipient delivery log, fans out over the
/// still-<see cref="RecipientStatus.Pending"/> recipients via <see cref="IEmailSender"/> (the fake sender
/// until cutover), and moves the campaign to a terminal state with tallies recomputed from the log.
///
/// <para><b>Resume-safe.</b> Each recipient's terminal state is persisted as it's processed, so a crash
/// mid-send leaves the already-sent recipients marked <see cref="RecipientStatus.Sent"/>. The
/// <see cref="CampaignSweepJob"/> detects the stuck campaign, resets it to Queued, and re-dispatches; this
/// job re-claims, finds the recipient rows already materialised, and processes only the remaining Pending
/// ones — so no recipient is sent twice across a resume.</para>
///
/// <para><b>No overlapping fan-out.</b> A run stops itself after <see cref="MaxSendDuration"/>, which is
/// strictly less than the sweep's <see cref="CampaignSweepJob.SendingStuckAfter"/> reset timeout. So a slow
/// run always bails (leaving the rest Pending for a fresh re-dispatch) before the sweep could reset its
/// campaign out from under it — there is never a second concurrent fan-out for the same campaign, and hence
/// no concurrent terminal write to race. (The atomic claim already serialises the start; the deadline
/// serialises the finish.)</para>
///
/// <para><b>At-least-once boundary.</b> A crash (or the deadline) <i>between</i> the email send and
/// persisting that one recipient's status can re-send exactly that recipient on resume. We favour delivery
/// over silent drop; provider-side dedupe is a cutover concern. One edge remains: a single send call that
/// hangs past the reset timeout (the per-recipient deadline is only checked between sends) could let the
/// sweep re-dispatch while it's still blocked — bounded at cutover by giving the real provider client its
/// own send timeout shorter than <see cref="MaxSendDuration"/>.</para>
///
/// <para>Enqueued by the API (via <see cref="ICampaignSendJob"/>) on send, and re-enqueued by the sweep.</para>
/// </summary>
public sealed class SendCampaignJob(
    RotationsDbContext db,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<SendCampaignJob> logger) : ICampaignSendJob
{
    /// <summary>How long a single run will fan out before bailing and leaving the rest for a re-dispatch.
    /// Must stay comfortably below <see cref="CampaignSweepJob.SendingStuckAfter"/> so a run always stops
    /// before the sweep would reset its campaign — guaranteeing one active fan-out per campaign at a time.</summary>
    public static readonly TimeSpan MaxSendDuration = TimeSpan.FromMinutes(10);

    public async Task SendAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        // Atomically claim the campaign (Queued → Sending) and stamp the claim time in a single conditional
        // UPDATE. If it affects 0 rows, it isn't claimable (already Sending/terminal, or gone) — so a
        // Hangfire retry, a re-delivery, or the sweep re-dispatch can never start a second concurrent fan-out
        // for a campaign that's already in flight. SendStartedAtUtc powers the sweep's stuck-send detection.
        var claimedAt = timeProvider.GetUtcNow();
        var claimed = await db.EmailCampaigns
            .Where(c => c.Id == campaignId && c.Status == CampaignStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, CampaignStatus.Sending)
                .SetProperty(c => c.SendStartedAtUtc, claimedAt), cancellationToken);
        if (claimed == 0)
        {
            logger.LogInformation(
                "SendCampaignJob: campaign {CampaignId} was not in a claimable Queued state; skipping.", campaignId);
            return;
        }

        // Re-load the now-Sending campaign (tracked) for its subject/body/audience and to write the tally.
        var campaign = await db.EmailCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning("SendCampaignJob: campaign {CampaignId} vanished after claim; skipping.", campaignId);
            return;
        }

        await MaterialiseRecipientsAsync(campaign, cancellationToken);

        // Process only the recipients not yet in a terminal state. On a first run that's everyone; on a
        // resume it's whatever was left when the previous run stopped.
        var pending = await db.CampaignRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == RecipientStatus.Pending)
            .ToListAsync(cancellationToken);

        var deadline = claimedAt + MaxSendDuration;
        var stoppedEarly = false;
        foreach (var recipient in pending)
        {
            // Bail before the sweep's reset window so two runs can never fan out the same campaign at once;
            // the untouched recipients stay Pending and a re-dispatch resumes them.
            if (timeProvider.GetUtcNow() >= deadline || cancellationToken.IsCancellationRequested)
            {
                stoppedEarly = true;
                break;
            }

            bool ok;
            try
            {
                ok = await emailSender.SendAsync(recipient.Email, campaign.Subject, campaign.Body, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stoppedEarly = true; // shutdown mid-send — stop cleanly, leave this and the rest Pending
                break;
            }
            catch (Exception ex)
            {
                // A sender that throws is a failed delivery, not a crash of the whole campaign — record a
                // classified (PII-free) reason and move on so one bad address can't strand the rest. We do
                // NOT persist ex.Message: a real provider's text can embed the recipient address or secrets.
                ok = false;
                recipient.Error = ex.GetType().Name;
            }

            if (ok)
            {
                recipient.Status = RecipientStatus.Sent;
                recipient.Error = null;
            }
            else
            {
                recipient.Status = RecipientStatus.Failed;
                recipient.Error ??= "Sender reported a failed delivery.";
            }
            recipient.AttemptedAtUtc = timeProvider.GetUtcNow();
            // Persist each recipient as it's processed so a crash preserves progress (resume-safety).
            await db.SaveChangesAsync(cancellationToken);
        }

        if (stoppedEarly)
        {
            // Leave Status=Sending / SendStartedAtUtc set: the sweep will re-dispatch after its timeout and a
            // fresh run resumes the remaining Pending recipients. Don't write a terminal state here.
            logger.LogWarning(
                "SendCampaignJob: campaign {CampaignId} stopped early (deadline or shutdown); remaining recipients left for the sweep to resume.",
                campaignId);
            return;
        }

        // Recompute the campaign tally from the delivery log (covers rows sent on an earlier, crashed run too)
        // and move to a terminal state. Failed only if there were recipients and none got through; a partial
        // failure still counts as Sent, with the failed tally surfaced for the admin.
        var counts = await db.CampaignRecipients
            .Where(r => r.CampaignId == campaignId)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var total = counts.Sum(c => c.Count);
        var sent = counts.FirstOrDefault(c => c.Key == RecipientStatus.Sent)?.Count ?? 0;
        var failed = counts.FirstOrDefault(c => c.Key == RecipientStatus.Failed)?.Count ?? 0;

        campaign.RecipientCount = total;
        campaign.SentCount = sent;
        campaign.FailedCount = failed;
        campaign.Status = total > 0 && sent == 0 ? CampaignStatus.Failed : CampaignStatus.Sent;
        campaign.SentAtUtc = timeProvider.GetUtcNow();
        campaign.SendStartedAtUtc = null; // no longer in flight — the sweep must not pick it up
        // Safe to write blind: the per-run deadline guarantees no other run is fanning out this campaign
        // concurrently, so there's no competing terminal write to clobber.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SendCampaignJob: campaign {CampaignId} → {Status} ({Sent} sent, {Failed} failed of {Total}).",
            campaignId, campaign.Status, sent, failed, total);
    }

    /// <summary>
    /// Inserts one <see cref="CampaignRecipient"/> per resolved audience member that doesn't already have a
    /// row for this campaign. Idempotent: a resume finds the rows already present and inserts nothing. The
    /// per-run deadline keeps fan-outs from overlapping, so this is normally uncontended; the unique
    /// <c>(CampaignId, Email)</c> index is a hard backstop and a concurrent insert race is tolerated.
    /// </summary>
    private async Task MaterialiseRecipientsAsync(EmailCampaign campaign, CancellationToken cancellationToken)
    {
        var emails = await ResolveRecipientsAsync(campaign.Audience, cancellationToken);

        var existing = await db.CampaignRecipients
            .Where(r => r.CampaignId == campaign.Id)
            .Select(r => r.Email)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = emails
            .Where(e => !existingSet.Contains(e))
            .Select(email => new CampaignRecipient
            {
                CampaignId = campaign.Id,
                Email = email,
                Status = RecipientStatus.Pending
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        db.CampaignRecipients.AddRange(toAdd);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            // A concurrent run materialised an overlapping recipient first (the unique index rejected the
            // insert). The rows exist either way — drop our duplicate inserts and continue; the Pending load
            // below will pick up whatever's there.
            foreach (var entry in db.ChangeTracker.Entries<CampaignRecipient>().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    /// <summary>Resolves an audience to its distinct recipient email addresses (over live rows).</summary>
    private async Task<List<string>> ResolveRecipientsAsync(EmailAudience audience, CancellationToken cancellationToken)
    {
        // Students who have booked at least one (live) rotation — the shared sub-query for both segments.
        var bookedStudentIds = db.Rotations.Where(r => r.StudentId != null).Select(r => r.StudentId!.Value);

        var query = audience switch
        {
            EmailAudience.AllStudents =>
                db.Students.Select(s => s.Email),
            EmailAudience.StudentsWithBooking =>
                db.Students.Where(s => bookedStudentIds.Contains(s.Id)).Select(s => s.Email),
            EmailAudience.StudentsWithoutBooking =>
                db.Students.Where(s => !bookedStudentIds.Contains(s.Id)).Select(s => s.Email),
            EmailAudience.AllPreceptors =>
                db.Preceptors.Select(p => p.Email),
            _ => null
        };

        return query is null
            ? []
            : await query.Distinct().ToListAsync(cancellationToken);
    }
}
