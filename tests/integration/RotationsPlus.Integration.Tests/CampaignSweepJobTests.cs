using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Crm;
using RotationsPlus.Contracts.Crm;
using RotationsPlus.Worker.Jobs;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// The Worker's <see cref="CampaignSweepJob"/>, exercised against the real (Testcontainers) DbContext with a
/// recording dispatcher and a fixed clock: it recovers campaigns stranded short of a terminal state
/// (a lost-enqueue Queued, or a crashed mid-Sending) and leaves healthy ones alone.
/// </summary>
public class CampaignSweepJobTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    /// <summary>Creates a campaign in a given state, optionally with an explicit claim time, and returns it.</summary>
    private async Task<EmailCampaign> SeedCampaignAsync(CampaignStatus status, DateTimeOffset? sendStartedAtUtc = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var campaign = new EmailCampaign
        {
            Subject = "Sweep test",
            Body = "Body",
            Audience = EmailAudience.AllStudents,
            Status = status,
            SendStartedAtUtc = sendStartedAtUtc
        };
        db.EmailCampaigns.Add(campaign);
        await db.SaveChangesAsync();
        return campaign;
    }

    private async Task<CampaignStatus> GetStatusAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return (await db.EmailCampaigns.AsNoTracking().FirstAsync(c => c.Id == id)).Status;
    }

    private async Task RunSweepAsync(RecordingCampaignDispatcher dispatcher, DateTimeOffset now)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var job = new CampaignSweepJob(db, dispatcher, new FixedTimeProvider(now), NullLogger<CampaignSweepJob>.Instance);
        await job.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_campaign_stuck_in_sending_is_reset_to_queued_and_redispatched()
    {
        var claimedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var campaign = await SeedCampaignAsync(CampaignStatus.Sending, claimedAt);

        var dispatcher = new RecordingCampaignDispatcher();
        // Now is well past the Sending timeout — the send crashed.
        await RunSweepAsync(dispatcher, claimedAt + CampaignSweepJob.SendingStuckAfter + TimeSpan.FromMinutes(1));

        (await GetStatusAsync(campaign.Id)).Should().Be(CampaignStatus.Queued);
        dispatcher.Dispatched.Should().Contain(campaign.Id);
    }

    [Fact]
    public async Task A_freshly_sending_campaign_is_left_alone()
    {
        var claimedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var campaign = await SeedCampaignAsync(CampaignStatus.Sending, claimedAt);

        var dispatcher = new RecordingCampaignDispatcher();
        // Only a minute in — still well within the timeout; a merely-slow send must not be disturbed.
        await RunSweepAsync(dispatcher, claimedAt + TimeSpan.FromMinutes(1));

        (await GetStatusAsync(campaign.Id)).Should().Be(CampaignStatus.Sending);
        dispatcher.Dispatched.Should().NotContain(campaign.Id);
    }

    [Fact]
    public async Task The_sending_cutoff_is_a_strict_boundary()
    {
        var claimedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        // Exactly at the cutoff: the predicate is `SendStartedAtUtc < cutoff` (strict), so a campaign
        // claimed exactly SendingStuckAfter ago is NOT yet stuck — left alone.
        var atBoundary = await SeedCampaignAsync(CampaignStatus.Sending, claimedAt);
        var d1 = new RecordingCampaignDispatcher();
        await RunSweepAsync(d1, claimedAt + CampaignSweepJob.SendingStuckAfter);
        d1.Dispatched.Should().NotContain(atBoundary.Id);
        (await GetStatusAsync(atBoundary.Id)).Should().Be(CampaignStatus.Sending);

        // Just past the cutoff: now stuck — recovered. (1ms, not 1 tick: Postgres `timestamp` resolves to
        // microseconds, so a sub-microsecond step would round back onto the boundary and not be "past".)
        var pastBoundary = await SeedCampaignAsync(CampaignStatus.Sending, claimedAt);
        var d2 = new RecordingCampaignDispatcher();
        await RunSweepAsync(d2, claimedAt + CampaignSweepJob.SendingStuckAfter + TimeSpan.FromMilliseconds(1));
        d2.Dispatched.Should().Contain(pastBoundary.Id);
        (await GetStatusAsync(pastBoundary.Id)).Should().Be(CampaignStatus.Queued);
    }

    [Fact]
    public async Task A_sending_campaign_with_no_claim_time_is_never_recovered()
    {
        // Defensive: SendStartedAtUtc is always set at claim, but the guard requires it — a null must not be
        // treated as "infinitely stale" and reset.
        var campaign = await SeedCampaignAsync(CampaignStatus.Sending, sendStartedAtUtc: null);

        var dispatcher = new RecordingCampaignDispatcher();
        await RunSweepAsync(dispatcher, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        dispatcher.Dispatched.Should().NotContain(campaign.Id);
        (await GetStatusAsync(campaign.Id)).Should().Be(CampaignStatus.Sending);
    }

    [Fact]
    public async Task A_campaign_stuck_in_queued_is_redispatched_without_changing_state()
    {
        var campaign = await SeedCampaignAsync(CampaignStatus.Queued);

        var dispatcher = new RecordingCampaignDispatcher();
        // The campaign was created "now"; advance past the Queued timeout so its enqueue looks lost.
        await RunSweepAsync(dispatcher, campaign.CreatedAtUtc + CampaignSweepJob.QueuedStuckAfter + TimeSpan.FromMinutes(1));

        dispatcher.Dispatched.Should().Contain(campaign.Id);
        (await GetStatusAsync(campaign.Id)).Should().Be(CampaignStatus.Queued); // still Queued — the job will claim it
    }

    [Fact]
    public async Task A_freshly_queued_campaign_is_left_alone()
    {
        var campaign = await SeedCampaignAsync(CampaignStatus.Queued);

        var dispatcher = new RecordingCampaignDispatcher();
        // Barely after creation — within the Queued grace window.
        await RunSweepAsync(dispatcher, campaign.CreatedAtUtc + TimeSpan.FromMinutes(1));

        dispatcher.Dispatched.Should().NotContain(campaign.Id);
    }

    [Fact]
    public void The_send_deadline_is_strictly_shorter_than_the_sweep_reset_timeout()
    {
        // The no-overlapping-fan-out guarantee depends entirely on a run bailing (after MaxSendDuration)
        // before the sweep would reset it (after SendingStuckAfter). Pin that ordering so a future timeout
        // tweak can't silently invert it.
        SendCampaignJob.MaxSendDuration.Should().BeLessThan(CampaignSweepJob.SendingStuckAfter);
    }

    [Fact]
    public async Task A_reset_sending_campaign_is_dispatched_once_not_twice_in_the_same_pass()
    {
        // Regression for the stale-ModifiedAtUtc bug: a campaign reset from Sending → Queued must not also
        // be re-matched by the Queued sweep in the SAME pass. The reset stamps ModifiedAtUtc=now, so the
        // Queued cutoff (now - 3min) can't match it. Without that stamp, its old CreatedAtUtc would.
        var campaign = await SeedCampaignAsync(CampaignStatus.Sending, sendStartedAtUtc: DateTimeOffset.UnixEpoch);

        var dispatcher = new RecordingCampaignDispatcher();
        // Far enough past creation that, absent the ModifiedAtUtc stamp, the just-reset row would satisfy the
        // Queued cutoff too.
        await RunSweepAsync(dispatcher, campaign.CreatedAtUtc + TimeSpan.FromMinutes(20));

        dispatcher.Dispatched.Count(id => id == campaign.Id).Should().Be(1); // exactly once, not twice
        (await GetStatusAsync(campaign.Id)).Should().Be(CampaignStatus.Queued);
    }

    [Fact]
    public async Task Terminal_campaigns_are_never_swept()
    {
        var sent = await SeedCampaignAsync(CampaignStatus.Sent);
        var failed = await SeedCampaignAsync(CampaignStatus.Failed);
        var draft = await SeedCampaignAsync(CampaignStatus.Draft);

        var dispatcher = new RecordingCampaignDispatcher();
        // Far in the future — even past every timeout, terminal/draft states are out of scope.
        await RunSweepAsync(dispatcher, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        dispatcher.Dispatched.Should().NotContain(new[] { sent.Id, failed.Id, draft.Id });
    }
}
