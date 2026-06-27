using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>
/// One recipient of a campaign send — the per-recipient delivery log behind <see cref="EmailCampaign"/>'s
/// aggregate counts. The send job materialises one row per resolved audience member (idempotently, keyed by
/// <c>(CampaignId, Email)</c>), then transitions each from <see cref="RecipientStatus.Pending"/> to a
/// terminal state as it sends. Because the job only processes <c>Pending</c> rows, a crashed send can be
/// re-dispatched and resumes from where it stopped without re-sending anyone already terminal.
///
/// Not an <c>AuditableEntity</c>: it's an append-only technical delivery log written by the Worker (which
/// has no audit interceptor), not a domain aggregate, and is never soft-deleted. It carries its own
/// explicit <see cref="AttemptedAtUtc"/> timestamp, stamped by the job from <c>TimeProvider</c>.
/// </summary>
public sealed class CampaignRecipient
{
    public Guid Id { get; set; }

    /// <summary>The owning campaign (<see cref="EmailCampaign.Id"/>).</summary>
    public Guid CampaignId { get; set; }

    /// <summary>The recipient's email address as resolved from the audience at materialisation time.</summary>
    public required string Email { get; set; }

    public RecipientStatus Status { get; set; } = RecipientStatus.Pending;

    /// <summary>The failure reason when <see cref="Status"/> is <see cref="RecipientStatus.Failed"/>; null
    /// otherwise. Kept short and free of PII beyond the email already stored on the row.</summary>
    public string? Error { get; set; }

    /// <summary>When the send was last attempted (terminal transition). Null while still Pending.</summary>
    public DateTimeOffset? AttemptedAtUtc { get; set; }
}
