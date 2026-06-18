namespace RotationsPlus.Contracts.Payments;

/// <summary>
/// The result of opening a deposit payment for a rotation. <see cref="ClientSecret"/> is what the SPA
/// hands to the payment provider's element to confirm the card; the server never sees card data. The
/// money figures echo the server-computed quote so the client can confirm what is being charged now
/// (<see cref="Amount"/>) vs. billed later (<see cref="OutstandingAmount"/>).
/// </summary>
public sealed record PaymentIntentResponse(
    Guid PaymentId,
    string ClientSecret,
    decimal Amount,
    decimal TotalAmount,
    decimal OutstandingAmount,
    string Currency,
    PaymentStatus Status);
