namespace RotationsPlus.Contracts.Payments;

/// <summary>
/// The three preceptor-payout milestones for a rotation (legacy parity). The full honorarium
/// (program weekly honorarium × weeks) is split across them: <see cref="Deposit"/> 25% at booking,
/// <see cref="Start"/> 25% at start, <see cref="Evaluation"/> 50% at completion + evaluation.
/// Stored as a string so the ordering of this enum can change without a data migration.
/// </summary>
public enum HonorariumStage
{
    Deposit,
    Start,
    Evaluation
}
