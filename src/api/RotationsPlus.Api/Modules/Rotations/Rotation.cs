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
    /// <summary>Short, human-facing sequential number (DB identity) shown to users as "R{number}" —
    /// the rewrite analog of the legacy integer rotation id. Server-assigned; never set from a request.</summary>
    public int RotationNumber { get; set; }

    public required Guid ProgramId { get; set; }
    public RotationProgram Program { get; set; } = null!;

    // The booked student. <see cref="StudentId"/> links to the directory record (nullable only for
    // legacy rows created before the directory existed); admins now pick a directory student. The
    // name/email/oid are a SNAPSHOT taken from that student at write time, so the rotation stays
    // self-contained for display even if the student is later edited or soft-deleted.
    public Guid? StudentId { get; set; }
    public required string StudentName { get; set; }
    public required string StudentEmail { get; set; }
    public string? StudentOid { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Whole weeks spanned, derived from the date range on write.</summary>
    public int Weeks { get; set; }

    public RotationStatus Status { get; set; } = RotationStatus.Pending;

    /// <summary>Admin-toggled flag (legacy <c>documents_approved</c>) shown as a checkbox on the
    /// dashboard's Upcoming-Starts row. A coarse "the student's paperwork is in order" marker the admin
    /// sets by hand — distinct from the granular per-document review in the documents subsystem.</summary>
    public bool DocumentsApproved { get; set; }

    /// <summary>Admin-toggled flag (legacy <c>preceptor_confirmed</c>) shown as a checkbox on the
    /// dashboard's Upcoming-Starts row — the preceptor has confirmed they will take the student.</summary>
    public bool PreceptorConfirmed { get; set; }
}
