using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>
/// An admin email campaign — a subject + body sent to a resolved <see cref="EmailAudience"/>. Composed as
/// a <see cref="CampaignStatus.Draft"/>, then queued for the Worker's send job, which fans out over the
/// audience via <c>IEmailSender</c> and tallies the result. The body is the (plain-text) message. The
/// aggregate counts are recomputed from the per-recipient delivery log (<see cref="CampaignRecipient"/>),
/// which the send job materialises and updates so a crashed send can resume without re-sending.
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

    /// <summary>When the send job claimed this campaign (Queued → Sending). Powers the sweep's stuck-send
    /// detection: a campaign still <see cref="CampaignStatus.Sending"/> long after this was set crashed
    /// mid-send and is reset to Queued for a resume. Cleared when the send reaches a terminal state.</summary>
    public DateTimeOffset? SendStartedAtUtc { get; set; }

    /// <summary>When the send job finished (reached a terminal state). Null until then.</summary>
    public DateTimeOffset? SentAtUtc { get; set; }
}
