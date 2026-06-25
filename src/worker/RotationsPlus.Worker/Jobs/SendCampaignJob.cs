using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Email;
using RotationsPlus.Common.Jobs;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Worker.Jobs;

/// <summary>
/// Sends a queued email campaign: resolves the audience to recipient emails, fans out over them via
/// <see cref="IEmailSender"/> (the fake sender until cutover), tallies sent/failed, and moves the
/// campaign to a terminal state. Idempotent — only a <see cref="CampaignStatus.Queued"/> campaign is
/// processed, so a re-delivered job (Hangfire retry) is a no-op. Enqueued by the API via
/// <see cref="ICampaignSendJob"/>; resolved here from the Worker's DI container.
/// </summary>
public sealed class SendCampaignJob(
    RotationsDbContext db,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<SendCampaignJob> logger) : ICampaignSendJob
{
    public async Task SendAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        // Atomically claim the campaign (Queued → Sending) in a single conditional UPDATE. If it affects
        // 0 rows, another run already claimed it (or it isn't sendable) — so a Hangfire retry, a
        // re-delivery, or a second Worker instance can never fan out the same campaign twice. This is the
        // at-least-once claim pattern; it replaces a read-then-write guard that two runs could both pass.
        var claimed = await db.EmailCampaigns
            .Where(c => c.Id == campaignId && c.Status == CampaignStatus.Queued)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CampaignStatus.Sending), cancellationToken);
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

        var recipients = await ResolveRecipientsAsync(campaign.Audience, cancellationToken);
        campaign.RecipientCount = recipients.Count;

        var sent = 0;
        var failed = 0;
        foreach (var email in recipients)
        {
            var ok = await emailSender.SendAsync(email, campaign.Subject, campaign.Body, cancellationToken);
            if (ok) sent++;
            else failed++;
        }

        campaign.SentCount = sent;
        campaign.FailedCount = failed;
        // Terminal: Failed only if nothing got through; a partial failure still counts as Sent (the
        // failed tally is surfaced so an admin can see it).
        campaign.Status = recipients.Count > 0 && sent == 0 ? CampaignStatus.Failed : CampaignStatus.Sent;
        campaign.SentAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SendCampaignJob: campaign {CampaignId} → {Status} ({Sent} sent, {Failed} failed of {Total}).",
            campaignId, campaign.Status, sent, failed, recipients.Count);
    }

    /// <summary>Resolves an audience to the set of recipient email addresses (over live rows).</summary>
    private async Task<List<string>> ResolveRecipientsAsync(EmailAudience audience, CancellationToken cancellationToken)
    {
        // Students who have booked at least one (live) rotation — the shared sub-query for both segments.
        var bookedStudentIds = db.Rotations.Where(r => r.StudentId != null).Select(r => r.StudentId!.Value);

        return audience switch
        {
            EmailAudience.AllStudents =>
                await db.Students.Select(s => s.Email).ToListAsync(cancellationToken),
            EmailAudience.StudentsWithBooking =>
                await db.Students.Where(s => bookedStudentIds.Contains(s.Id)).Select(s => s.Email).ToListAsync(cancellationToken),
            EmailAudience.StudentsWithoutBooking =>
                await db.Students.Where(s => !bookedStudentIds.Contains(s.Id)).Select(s => s.Email).ToListAsync(cancellationToken),
            EmailAudience.AllPreceptors =>
                await db.Preceptors.Select(p => p.Email).ToListAsync(cancellationToken),
            _ => []
        };
    }
}
