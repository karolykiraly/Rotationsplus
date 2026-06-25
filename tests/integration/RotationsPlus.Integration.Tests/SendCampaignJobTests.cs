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

    private async Task RunJobAsync(Guid campaignId, RecordingEmailSender sender)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var job = new SendCampaignJob(db, sender, TimeProvider.System, NullLogger<SendCampaignJob>.Instance);
        await job.SendAsync(campaignId, CancellationToken.None);
    }

    private async Task<EmailCampaign> GetCampaignAsync(Guid campaignId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.EmailCampaigns.AsNoTracking().FirstAsync(c => c.Id == campaignId);
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
}
