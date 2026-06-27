namespace RotationsPlus.Contracts.Payments;

/// <summary>
/// One honorarium row on the admin honorarium screen (a single payout stage for one rotation). The
/// preceptor/student names and rotation number/start date are snapshots taken when the schedule was
/// generated, so the row renders self-contained without joins. The same shape is returned by the
/// pay / refund-flag actions.
/// </summary>
public sealed record HonorariumResponse(
    Guid Id,
    Guid RotationId,
    int RotationNumber,
    Guid? PreceptorId,
    string PreceptorName,
    string StudentName,
    HonorariumStage Stage,
    decimal Amount,
    string Currency,
    HonorariumStatus Status,
    bool Refunded,
    DateOnly RotationStartDate,
    DateOnly? EvaluationDueDate,
    DateTimeOffset? PaidAtUtc);

/// <summary>Toggles the independent "refunded" bookkeeping flag on a honorarium row (legacy checkbox).</summary>
public sealed record SetHonorariumRefundRequest(bool Refunded);
