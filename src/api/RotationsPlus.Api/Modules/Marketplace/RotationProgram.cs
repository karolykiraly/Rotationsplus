using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// A clinical-rotation program offering in the marketplace catalog (legacy <c>program</c>, cleaned).
/// Named <c>RotationProgram</c> to avoid colliding with the host's top-level <c>Program</c> class.
/// Models the specialty, the offering preceptor (optional until assigned), delivery type, capacity,
/// and pricing.
/// </summary>
public sealed class RotationProgram : AuditableEntity
{
    public required Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    /// <summary>The preceptor who offers this program. Optional — a catalog program may exist before
    /// a preceptor is assigned (and is assigned/changed by an admin).</summary>
    public Guid? PreceptorId { get; set; }
    public Preceptor? Preceptor { get; set; }

    public required ProgramType ProgramType { get; set; }

    /// <summary>A short, human-facing sequential number (DB identity) shown to users as a typed code
    /// (e.g. "IP1042") — the rewrite analog of the legacy integer <c>program_id</c>. Server-assigned;
    /// never set from a request.</summary>
    public int ProgramNumber { get; set; }

    /// <summary>Program location (city / US state). Optional — populated from the production data
    /// migration; the rewrite seed leaves sample values.</summary>
    public string? City { get; set; }
    public string? State { get; set; }

    /// <summary>Free-form marketplace tags shown as chips (e.g. "Hospital Letterhead LOR", "Research").
    /// "Instant Approval" is NOT stored here — it is derived from <see cref="IsOpen"/> at projection
    /// time so the flag and the tag can never disagree.</summary>
    public List<string> Tags { get; set; } = [];

    public int MaxStudentsPerRotation { get; set; }
    public int MinWeeksPerRotation { get; set; }

    public decimal RetailAmountPerWeek { get; set; }
    public decimal WeeklyHonorarium { get; set; }

    /// <summary>Whether this is an "open" (instant-approval) program. Open programs are charged in
    /// full (100%) at checkout; non-open programs take a deposit (see <c>PricingService</c>) with the
    /// balance billed as an outstanding payment. Mirrors the legacy open-vs-manual availability mode.</summary>
    public bool IsOpen { get; set; }

    public string? Description { get; set; }
}
