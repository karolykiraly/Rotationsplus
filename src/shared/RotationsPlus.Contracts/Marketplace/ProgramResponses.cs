namespace RotationsPlus.Contracts.Marketplace;

/// <summary>A clinical-rotation program as shown in the catalog list. <c>PreceptorName</c> is null
/// when no preceptor is assigned yet.</summary>
public sealed record ProgramSummaryResponse(
    Guid Id,
    string SpecialtyName,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    string? PreceptorName);

/// <summary>Full detail for a single clinical-rotation program. <c>WeeklyHonorarium</c> (the
/// preceptor's pay, i.e. platform margin vs. the retail price) is staff-only — it is null for
/// customer callers so students/preceptors can't infer margin.</summary>
public sealed record ProgramDetailResponse(
    Guid Id,
    Guid SpecialtyId,
    string SpecialtyName,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal? WeeklyHonorarium,
    string? Description,
    Guid? PreceptorId,
    string? PreceptorName);
