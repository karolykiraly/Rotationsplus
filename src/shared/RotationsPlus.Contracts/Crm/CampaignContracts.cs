namespace RotationsPlus.Contracts.Crm;

/// <summary>Who an email campaign is sent to. Resolved to concrete recipients at send time.</summary>
public enum EmailAudience
{
    AllStudents,
    StudentsWithBooking,
    StudentsWithoutBooking,
    AllPreceptors
}

/// <summary>The lifecycle of an email campaign: composed → queued → sending → terminal (sent/failed).</summary>
public enum CampaignStatus
{
    Draft,
    Queued,
    Sending,
    Sent,
    Failed
}

/// <summary>Per-recipient delivery state within a campaign send. A recipient stays <see cref="Pending"/>
/// until the send job processes it, then becomes terminal (<see cref="Sent"/>/<see cref="Failed"/>). The
/// job only ever processes <see cref="Pending"/> rows, so a re-run (resume after a crash) never re-sends a
/// recipient already marked terminal.</summary>
public enum RecipientStatus
{
    Pending,
    Sent,
    Failed
}

/// <summary>Admin payload to compose a campaign (saved as a Draft).</summary>
public sealed record CreateCampaignRequest(string Subject, string Body, EmailAudience Audience);

/// <summary>A campaign row for the list view (no body).</summary>
public sealed record CampaignSummaryResponse(
    Guid Id,
    string Subject,
    EmailAudience Audience,
    CampaignStatus Status,
    int RecipientCount,
    int SentCount,
    int FailedCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc);

/// <summary>A campaign with its full body, for the detail/compose-review view.</summary>
public sealed record CampaignDetailResponse(
    Guid Id,
    string Subject,
    string Body,
    EmailAudience Audience,
    CampaignStatus Status,
    int RecipientCount,
    int SentCount,
    int FailedCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc);
