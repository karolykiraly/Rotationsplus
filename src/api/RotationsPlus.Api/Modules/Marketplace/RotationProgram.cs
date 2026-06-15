using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// A clinical-rotation program offering in the marketplace catalog (legacy <c>program</c>, cleaned).
/// Named <c>RotationProgram</c> to avoid colliding with the host's top-level <c>Program</c> class.
/// The preceptor association is added in the Preceptor slice; this slice models the specialty,
/// delivery type, capacity, and pricing.
/// </summary>
public sealed class RotationProgram : AuditableEntity
{
    public required Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    public required ProgramType ProgramType { get; set; }

    public int MaxStudentsPerRotation { get; set; }
    public int MinWeeksPerRotation { get; set; }

    public decimal RetailAmountPerWeek { get; set; }
    public decimal WeeklyHonorarium { get; set; }

    public string? Description { get; set; }
}
