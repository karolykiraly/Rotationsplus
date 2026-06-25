using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>
/// An admin email campaign — a subject + body sent to a resolved <see cref="EmailAudience"/>. Composed as
/// a <see cref="CampaignStatus.Draft"/>, then queued for the Worker's send job, which fans out over the
/// audience via <c>IEmailSender</c> and tallies the result. The recipient/sent/failed counts are filled
/// by the job; the body is the (plain-text) message. The first slice models aggregate counts only — a
/// per-recipient delivery log is a later add.
/// </summary>
public sealed class EmailCampaign : AuditableEntity
{
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public EmailAudience Audience { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>When the send job finished (reached a terminal state). Null until then.</summary>
    public DateTimeOffset? SentAtUtc { get; set; }
}
