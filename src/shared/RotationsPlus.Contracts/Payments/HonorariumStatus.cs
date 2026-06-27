namespace RotationsPlus.Contracts.Payments;

/// <summary>
/// Payout state of a single honorarium stage. Created <see cref="Pending"/> when the rotation's payout
/// schedule is generated (on deposit success); an admin marks it <see cref="Paid"/> from the honorarium
/// screen. <see cref="Cancelled"/> is reserved for a rotation that falls through before the stage is paid
/// (not yet wired — cancellation handling is a later slice). The independent "refunded" bookkeeping flag
/// is a separate boolean, mirroring the legacy refunded checkbox.
/// </summary>
public enum HonorariumStatus
{
    Pending,
    Paid,
    Cancelled
}
