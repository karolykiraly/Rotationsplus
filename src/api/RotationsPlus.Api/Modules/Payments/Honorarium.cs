using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// One preceptor-payout stage for a <see cref="Rotation"/>. Three are generated per rotation when its
/// deposit succeeds (<see cref="HonorariumStage.Deposit"/> 25% / <see cref="HonorariumStage.Start"/> 25% /
/// <see cref="HonorariumStage.Evaluation"/> 50% of program weekly honorarium × weeks), and an admin marks
/// each <see cref="HonorariumStatus.Paid"/> from the honorarium screen as the milestone is reached.
///
/// The preceptor/student names and the rotation number/start date are SNAPSHOTS captured at generation
/// (mirroring how <see cref="Rotation"/> snapshots its student), so a payout row stays self-contained and
/// renders without joins even if the underlying records are later edited. Marking a stage paid is
/// bookkeeping only — the actual disbursement to the preceptor is handled outside the system today; no
/// gateway money movement happens here.
/// </summary>
public sealed class Honorarium : AuditableEntity
{
    public required Guid RotationId { get; set; }
    public Rotation Rotation { get; set; } = null!;

    /// <summary>The paid preceptor (the program's preceptor at generation). Nullable because a catalog
    /// program may have had no preceptor assigned; the name snapshot still renders the row.</summary>
    public Guid? PreceptorId { get; set; }

    public required string PreceptorName { get; set; }
    public required string StudentName { get; set; }

    /// <summary>The rotation's human-facing sequential number, snapshotted for "R{number}" display.</summary>
    public int RotationNumber { get; set; }
    public DateOnly RotationStartDate { get; set; }

    /// <summary>The evaluation due date shown on the Evaluation-tab column (legacy <c>rotation.due_date</c>):
    /// a snapshot of the rotation end date plus the legacy 7-day grace (end_date + 7d), captured at
    /// generation like the other snapshots. Nullable so honorarium rows generated before this column existed
    /// render "-" (matching the legacy fallback) rather than a wrong date.</summary>
    public DateOnly? EvaluationDueDate { get; set; }

    public required HonorariumStage Stage { get; set; }

    public required decimal Amount { get; set; }
    public required string Currency { get; set; }

    public HonorariumStatus Status { get; set; } = HonorariumStatus.Pending;

    /// <summary>Independent "refunded" bookkeeping flag (legacy checkbox) — does not change
    /// <see cref="Status"/>; just records that this payout was/should be reversed.</summary>
    public bool Refunded { get; set; }

    /// <summary>When the stage was marked paid, and the admin (oid) who marked it. Null until paid.</summary>
    public DateTimeOffset? PaidAtUtc { get; set; }
    public string? PaidBy { get; set; }
}
