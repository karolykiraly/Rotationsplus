namespace RotationsPlus.Common.Domain;

/// <summary>Entity that records who/when it was created and last modified (stamped by the audit interceptor).</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? ModifiedAtUtc { get; set; }
    string? ModifiedBy { get; set; }
}

/// <summary>Entity that is never hard-deleted; a delete flips <see cref="IsDeleted"/> and is filtered out of queries.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>
/// Base for persisted aggregate roots: a sequential GUID key plus audit + soft-delete columns.
/// Timestamps and the acting user are filled by the audit interceptor on SaveChanges — domain
/// code never sets them directly (no <c>DateTime.UtcNow</c>; see CLAUDE.md §4).
/// </summary>
public abstract class AuditableEntity : IAuditable, ISoftDeletable
{
    // Version-7 GUIDs are time-ordered, giving B-tree index locality close to a sequential key
    // while staying client-generatable. (.NET 9 Guid.CreateVersion7.)
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
