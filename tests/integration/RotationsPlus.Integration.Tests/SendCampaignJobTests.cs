using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Crm;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Crm;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;
using RotationsPlus.Worker.Jobs;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// The Worker's <see cref="SendCampaignJob"/>, exercised against the real (Testcontainers) DbContext with
/// a recording email sender: it resolves the audience, fans out, tallies, and moves the campaign to a
/// terminal state — and is idempotent for a re-delivered job.
/// </summary>
public class SendCampaignJobTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid ProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-admin");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Admin);
        return client;
    }

    private async Task<(Guid id, string email)> CreateStudentAsync(HttpClient admin)
    {
        var email = $"camp.{Guid.NewGuid():N}@example.com";
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Camp", "Student", email, null, AcademicStatus.MdStudent,
                    null, null, null, null, null, StudentStatus.MemberActivated, $"ciam-{Guid.NewGuid():N}"), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return (student!.Id, email);
    }

    private async Task BookRotationAsync(HttpClient admin, Guid studentId)
    {
        (await admin.PostAsJsonAsync("/api/rotations",
            new CreateRotationRequest(ProgramId, studentId,
                new DateOnly(2027, 6, 7), new DateOnly(2027, 7, 5), RotationStatus.NotStarted), JsonOptions))
            .EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateCampaignAsync(EmailAudience audience, CampaignStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var campaign = new EmailCampaign
        {
            Subject = "Test campaign",
            Body = "Hello from the campaign.",
            Audience = audience,
            Status = status
        };
        db.EmailCampaigns.Add(campaign);
        await db.SaveChangesAsync();
        return campaign.Id;
    }

    private async Task RunJobAsync(Guid campaignId, RecordingEmailSender sender, TimeProvider? timeProvider = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var job = new SendCampaignJob(db, sender, timeProvider ?? TimeProvider.System, NullLogger<SendCampaignJob>.Instance);
        await job.SendAsync(campaignId, CancellationToken.None);
    }

    private async Task<EmailCampaign> GetCampaignAsync(Guid campaignId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.EmailCampaigns.AsNoTracking().FirstAsync(c => c.Id == campaignId);
    }

    private async Task<List<CampaignRecipient>> GetRecipientsAsync(Guid campaignId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.CampaignRecipients.AsNoTracking()
            .Where(r => r.CampaignId == campaignId).ToListAsync();
    }

    /// <summary>Pre-seeds a per-recipient row (e.g. an already-Sent recipient from a prior, crashed run).</summary>
    private async Task SeedRecipientAsync(Guid campaignId, string email, RecipientStatus status, DateTimeOffset? attemptedAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        db.CampaignRecipients.Add(new CampaignRecipient
        {
            CampaignId = campaignId,
            Email = email,
            Status = status,
            AttemptedAtUtc = attemptedAtUtc
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Sending_to_all_students_fans_out_and_completes()
    {
        var admin = Admin();
        var (_, email) = await CreateStudentAsync(admin);
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        var sender = new RecordingEmailSender();
        await RunJobAsync(campaignId, sender);

        var campaign = await GetCampaignAsync(campaignId);
        campaign.Status.Should().Be(CampaignStatus.Sent);
        campaign.RecipientCount.Should().Be(campaign.SentCount);
        campaign.FailedCount.Should().Be(0);
        campaign.RecipientCount.Should().BeGreaterThanOrEqualTo(1);
        campaign.SentAtUtc.Should().NotBeNull();
        sender.Sent.Should().Contain(email);
        sender.Sent.Count.Should().Be(campaign.RecipientCount);
    }

    [Fact]
    public async Task Audience_segmentation_splits_booked_from_unbooked_students()
    {
        var admin = Admin();
        var (bookedId, bookedEmail) = await CreateStudentAsync(admin);
        await BookRotationAsync(admin, bookedId);
        var (_, unbookedEmail) = await CreateStudentAsync(admin);

        var withBooking = await CreateCampaignAsync(EmailAudience.StudentsWithBooking, CampaignStatus.Queued);
        var withoutBooking = await CreateCampaignAsync(EmailAudience.StudentsWithoutBooking, CampaignStatus.Queued);

        var withSender = new RecordingEmailSender();
        var withoutSender = new RecordingEmailSender();
        await RunJobAsync(withBooking, withSender);
        await RunJobAsync(withoutBooking, withoutSender);

        withSender.Sent.Should().Contain(bookedEmail).And.NotContain(unbookedEmail);
        withoutSender.Sent.Should().Contain(unbookedEmail).And.NotContain(bookedEmail);
    }

    [Fact]
    public async Task A_send_where_every_recipient_fails_ends_in_failed()
    {
        var admin = Admin();
        await CreateStudentAsync(admin); // ensure at least one recipient
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        var sender = new RecordingEmailSender(succeed: false); // every send reports failure
        await RunJobAsync(campaignId, sender);

        var campaign = await GetCampaignAsync(campaignId);
        campaign.Status.Should().Be(CampaignStatus.Failed);
        campaign.FailedCount.Should().Be(campaign.RecipientCount);
        campaign.SentCount.Should().Be(0);
        // A terminal campaign (even Failed) is no longer in flight — the sweep must not pick it up.
        campaign.SendStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task A_partial_failure_still_completes_as_sent_with_the_failed_tally()
    {
        // One recipient fails, the rest succeed → the campaign is Sent (some got through) but the failed
        // count is surfaced. This is the boundary the terminal-state ternary turns on.
        var admin = Admin();
        var (_, failEmail) = await CreateStudentAsync(admin);
        var (_, okEmail) = await CreateStudentAsync(admin);
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        var sender = new RecordingEmailSender();
        sender.FailFor.Add(failEmail);
        await RunJobAsync(campaignId, sender);

        var recipients = await GetRecipientsAsync(campaignId);
        recipients.Single(r => r.Email == failEmail).Status.Should().Be(RecipientStatus.Failed);
        recipients.Single(r => r.Email == okEmail).Status.Should().Be(RecipientStatus.Sent);

        var campaign = await GetCampaignAsync(campaignId);
        campaign.Status.Should().Be(CampaignStatus.Sent); // partial failure ≠ Failed
        campaign.FailedCount.Should().BeGreaterThanOrEqualTo(1);
        campaign.SentCount.Should().BeGreaterThanOrEqualTo(1);
        campaign.RecipientCount.Should().Be(campaign.SentCount + campaign.FailedCount);
    }

    [Fact]
    public async Task A_sender_that_throws_marks_that_recipient_failed_without_leaking_pii_and_continues()
    {
        var admin = Admin();
        var (_, throwEmail) = await CreateStudentAsync(admin);
        var (_, okEmail) = await CreateStudentAsync(admin);
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        var sender = new RecordingEmailSender();
        sender.ThrowFor.Add(throwEmail); // the sender blows up (its message embeds the address)
        await RunJobAsync(campaignId, sender);

        var recipients = await GetRecipientsAsync(campaignId);
        var thrown = recipients.Single(r => r.Email == throwEmail);
        thrown.Status.Should().Be(RecipientStatus.Failed);
        thrown.Error.Should().NotBeNullOrEmpty();
        thrown.Error.Should().NotContain(throwEmail); // PII-free: the recipient address is never persisted
        thrown.Error.Should().Be(nameof(InvalidOperationException)); // a classified, non-PII reason

        // The throw doesn't strand the rest — a later recipient still goes out and the campaign completes.
        recipients.Single(r => r.Email == okEmail).Status.Should().Be(RecipientStatus.Sent);
        (await GetCampaignAsync(campaignId)).Status.Should().Be(CampaignStatus.Sent);
    }

    [Fact]
    public async Task A_run_that_exceeds_its_deadline_bails_and_leaves_work_for_the_sweep()
    {
        // The per-run deadline (MaxSendDuration) is what guarantees a slow run stops before the sweep could
        // reset it — preventing overlapping fan-outs. Simulate the clock jumping past the deadline right
        // after the claim: the loop must bail without sending, leaving the campaign Sending (in flight) for
        // the sweep to re-dispatch.
        var admin = Admin();
        await CreateStudentAsync(admin); // at least one recipient to (not) process
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        var claimedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var pastDeadline = claimedAt + SendCampaignJob.MaxSendDuration + TimeSpan.FromMinutes(1);
        var sender = new RecordingEmailSender();
        await RunJobAsync(campaignId, sender, new SteppedTimeProvider(claimedAt, pastDeadline));

        sender.Sent.Should().BeEmpty(); // bailed before sending anyone

        var campaign = await GetCampaignAsync(campaignId);
        campaign.Status.Should().Be(CampaignStatus.Sending); // still in flight — not marked terminal
        campaign.SendStartedAtUtc.Should().NotBeNull();      // the sweep keys recovery off this

        // The recipients were materialised but left Pending for the resumed run.
        (await GetRecipientsAsync(campaignId)).Should().OnlyContain(r => r.Status == RecipientStatus.Pending);
    }

    [Fact]
    public async Task Sending_to_all_preceptors_targets_preceptor_emails()
    {
        var admin = Admin();
        var specialty = await (await admin.PostAsJsonAsync("/api/specialties",
                new CreateSpecialtyRequest($"Sweepology {Guid.NewGuid():N}"), JsonOptions))
            .Content.ReadFromJsonAsync<SpecialtyResponse>(JsonOptions);
        var preceptorEmail = $"prec.{Guid.NewGuid():N}@example.com";
        (await admin.PostAsJsonAsync("/api/preceptors",
            new CreatePreceptorRequest("Pat", "Cept", preceptorEmail, specialty!.Id,
                null, null, null, null, PreceptorStatus.Registered, null), JsonOptions))
            .EnsureSuccessStatusCode();
        var campaignId = await CreateCampaignAsync(EmailAudience.AllPreceptors, CampaignStatus.Queued);

        var sender = new RecordingEmailSender();
        await RunJobAsync(campaignId, sender);

        sender.Sent.Should().Contain(preceptorEmail);
        (await GetRecipientsAsync(campaignId)).Should().Contain(r => r.Email == preceptorEmail);
    }

    [Fact]
    public async Task A_non_queued_campaign_is_a_no_op_idempotency()
    {
        // Already terminal (Sent) — a re-delivered job must not re-send or change the tallies.
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Sent);

        var sender = new RecordingEmailSender();
        await RunJobAsync(campaignId, sender);

        sender.Sent.Should().BeEmpty();
        var campaign = await GetCampaignAsync(campaignId);
        campaign.Status.Should().Be(CampaignStatus.Sent);
        campaign.SentCount.Should().Be(0); // untouched
    }

    [Fact]
    public async Task Sending_materialises_one_recipient_row_per_audience_member()
    {
        var admin = Admin();
        var (_, email) = await CreateStudentAsync(admin);
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        await RunJobAsync(campaignId, new RecordingEmailSender());

        var recipients = await GetRecipientsAsync(campaignId);
        recipients.Should().Contain(r => r.Email == email);
        recipients.Should().OnlyContain(r => r.Status == RecipientStatus.Sent);
        recipients.Should().OnlyContain(r => r.AttemptedAtUtc != null);
        // The campaign tally is recomputed from the delivery log.
        var campaign = await GetCampaignAsync(campaignId);
        campaign.RecipientCount.Should().Be(recipients.Count);
        campaign.SentCount.Should().Be(recipients.Count);
        // No row should be Sending-orphaned: the campaign isn't left mid-flight.
        campaign.SendStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Resuming_a_crashed_send_does_not_resend_already_sent_recipients()
    {
        // Simulate a prior run that sent to `alreadySent` and then crashed (campaign left Queued for the
        // sweep to re-dispatch). The resumed run must skip the already-Sent recipient and only send the rest.
        var admin = Admin();
        var (_, alreadySent) = await CreateStudentAsync(admin);
        var (_, fresh) = await CreateStudentAsync(admin);
        var campaignId = await CreateCampaignAsync(EmailAudience.AllStudents, CampaignStatus.Queued);

        var seededAttempt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await SeedRecipientAsync(campaignId, alreadySent, RecipientStatus.Sent, seededAttempt);

        var sender = new RecordingEmailSender();
        await RunJobAsync(campaignId, sender);

        sender.Sent.Should().NotContain(alreadySent); // the crux: no double-send on resume
        sender.Sent.Should().Contain(fresh);

        var recipients = await GetRecipientsAsync(campaignId);
        // The pre-sent row is untouched (same attempt timestamp), proving it wasn't reprocessed.
        recipients.Single(r => r.Email == alreadySent).AttemptedAtUtc.Should().Be(seededAttempt);

        var campaign = await GetCampaignAsync(campaignId);
        campaign.Status.Should().Be(CampaignStatus.Sent);
        campaign.SentCount.Should().Be(recipients.Count); // every recipient ends Sent (none lost, none doubled)
    }
}
