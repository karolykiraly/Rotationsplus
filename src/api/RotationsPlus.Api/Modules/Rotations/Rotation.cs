using RotationsPlus.Api.Modules.Marketplace;
using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Rotations;

/// <summary>
/// A rotation: a student booked into a marketplace <see cref="RotationProgram"/> over a date range,
/// moving through the <see cref="RotationStatus"/> lifecycle. The preceptor is the program's
/// preceptor (no separate FK). This first slice models the core booking + status; the transition
/// state machine, documents, evaluation, and payments arrive in later slices (see Plan_Admin.md §2).
/// </summary>
public sealed class Rotation : AuditableEntity
{
    public required Guid ProgramId { get; set; }
    public RotationProgram Program { get; set; } = null!;

    // Denormalized student identity. A Student directory/profile (linked by CIAM oid) is a later
    // slice; for now an admin records the student's name/email when creating the rotation, and
    // StudentOid links to the CIAM account once known.
    public required string StudentName { get; set; }
    public required string StudentEmail { get; set; }
    public string? StudentOid { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Whole weeks spanned, derived from the date range on write.</summary>
    public int Weeks { get; set; }

    public RotationStatus Status { get; set; } = RotationStatus.Pending;
}
