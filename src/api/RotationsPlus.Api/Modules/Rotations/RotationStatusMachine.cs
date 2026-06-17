using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Rotations;

/// <summary>
/// The rotation lifecycle state machine. Governs which <see cref="RotationStatus"/> transitions an admin
/// may make on update, so the booking can't jump illegally (e.g. a Completed rotation back to Pending).
/// Create may record any status (the admin captures the rotation's real current state, incl. imports);
/// the graph constrains <em>changes</em> only. Staying on the same status is always allowed.
///
/// Forward flow (proposed default — adjust here if the business rules differ):
///   Pending       → NotStarted (approve), Rejected, Cancelled
///   NotStarted    → Active (start), Cancelled, Abandoned
///   Active        → ToBeEvaluated, Completed, Abandoned, Cancelled
///   ToBeEvaluated → Completed, Abandoned
///   Completed     → Refunded
///   Cancelled     → Refunded
///   Rejected / Refunded / Abandoned → terminal (no further changes)
/// </summary>
public static class RotationStatusMachine
{
    private static readonly IReadOnlyDictionary<RotationStatus, RotationStatus[]> Transitions =
        new Dictionary<RotationStatus, RotationStatus[]>
        {
            [RotationStatus.Pending] = [RotationStatus.NotStarted, RotationStatus.Rejected, RotationStatus.Cancelled],
            [RotationStatus.NotStarted] = [RotationStatus.Active, RotationStatus.Cancelled, RotationStatus.Abandoned],
            [RotationStatus.Active] = [RotationStatus.ToBeEvaluated, RotationStatus.Completed, RotationStatus.Abandoned, RotationStatus.Cancelled],
            [RotationStatus.ToBeEvaluated] = [RotationStatus.Completed, RotationStatus.Abandoned],
            [RotationStatus.Completed] = [RotationStatus.Refunded],
            [RotationStatus.Cancelled] = [RotationStatus.Refunded],
            [RotationStatus.Rejected] = [],
            [RotationStatus.Refunded] = [],
            [RotationStatus.Abandoned] = [],
        };

    /// <summary>The statuses a rotation in <paramref name="from"/> may move to (excludes <paramref name="from"/> itself).</summary>
    public static IReadOnlyList<RotationStatus> NextFrom(RotationStatus from) =>
        Transitions.TryGetValue(from, out var next) ? next : [];

    /// <summary>True if a rotation may move from <paramref name="from"/> to <paramref name="to"/> (a no-op stay is allowed).</summary>
    public static bool CanTransition(RotationStatus from, RotationStatus to) =>
        from == to || NextFrom(from).Contains(to);
}
