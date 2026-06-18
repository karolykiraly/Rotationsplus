namespace RotationsPlus.Contracts.Payments;

/// <summary>
/// A server-computed price quote for booking a program for a number of weeks. The client only
/// displays these figures — pricing rules live server-side (see <c>PricingService</c>) so the deposit
/// math has a single, tested source of truth. All amounts are in <see cref="Currency"/> (USD), to the
/// cent. <see cref="DepositAmount"/> is what is charged at checkout; <see cref="OutstandingAmount"/>
/// is billed later (zero for open / instant-approval programs, which are charged in full).
/// </summary>
public sealed record RotationQuoteResponse(
    Guid ProgramId,
    int Weeks,
    string Currency,
    decimal RetailAmountPerWeek,
    decimal TotalAmount,
    decimal DepositAmount,
    decimal OutstandingAmount,
    decimal DepositPercent,
    bool IsOpen);
