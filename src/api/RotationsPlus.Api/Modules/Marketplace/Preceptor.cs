using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// A preceptor directory record in the marketplace catalog — the supervising clinician who hosts
/// rotations. This first slice models the identity + professional core and a primary specialty;
/// onboarding (availability, documents, bank info), program associations, and the approval flow
/// arrive in later slices (see Plan_Preceptor.md).
/// </summary>
public sealed class Preceptor : AuditableEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }

    public required Guid PrimarySpecialtyId { get; set; }
    public Specialty PrimarySpecialty { get; set; } = null!;

    public string? MedicalLicenseNumber { get; set; }
    public string? LicenseState { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    /// <summary>Contact mobile number (legacy <c>mobile_phone</c>) — shown on the admin Permission screen.</summary>
    public string? MobilePhone { get; set; }

    /// <summary>Whether an onboarding call has been scheduled with this preceptor (legacy
    /// <c>call_scheduled</c>) — the Permission screen's "Scheduled" Yes/No column. The flow that sets it
    /// (call booking) is a post-cutover improvement; defaults to false ("No") for now.</summary>
    public bool CallScheduled { get; set; }

    public PreceptorStatus Status { get; set; } = PreceptorStatus.Registered;

    public string? Bio { get; set; }

    // ---- Admin approval queue (/admin/permission) audit ----
    // Who actioned the approve/reject (the reviewer's oid) and when; the reason captured on rejection.
    // Null until the preceptor has been through the queue. (Agreement/W9 handling is a later slice — §3.15a.)
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}
