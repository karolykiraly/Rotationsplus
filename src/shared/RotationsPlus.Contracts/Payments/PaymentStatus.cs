namespace RotationsPlus.Contracts.Payments;

/// <summary>
/// Lifecycle of a single payment against a rotation. A payment is created <see cref="Pending"/> when
/// the deposit intent is opened; the provider webhook moves it to <see cref="Succeeded"/> (fulfilment)
/// or <see cref="Failed"/>. <see cref="Refunded"/> is set when a charge is later refunded.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded
}
