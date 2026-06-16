namespace RotationsPlus.Contracts.Rotations;

/// <summary>
/// Rotation lifecycle status — cleaned from the legacy rotation status vocabulary
/// (Active, Approved/NotStarted, Pending, To-Be-Evaluated, Completed, Cancelled, Refund, Abandoned,
/// Rejected). The student tracker displays <see cref="NotStarted"/> as "Approved"; admins see the raw
/// status. <see cref="Cancelled"/>/<see cref="Refunded"/>/<see cref="Abandoned"/>/<see cref="Rejected"/>
/// are the terminal exception states (hidden from the student tracker). The transition rules /
/// state machine arrive in a later slice; this enum is the vocabulary.
/// </summary>
public enum RotationStatus
{
    Pending,
    NotStarted,
    Active,
    ToBeEvaluated,
    Completed,
    Cancelled,
    Refunded,
    Abandoned,
    Rejected
}
