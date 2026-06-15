namespace RotationsPlus.Contracts.Marketplace;

/// <summary>A clinical-rotation program as shown in the catalog list.</summary>
public sealed record ProgramSummaryResponse(
    Guid Id,
    string SpecialtyName,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek);

/// <summary>Full detail for a single clinical-rotation program.</summary>
public sealed record ProgramDetailResponse(
    Guid Id,
    Guid SpecialtyId,
    string SpecialtyName,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description);
