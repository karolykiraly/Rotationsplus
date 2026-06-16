namespace RotationsPlus.Contracts.Marketplace;

/// <summary>Admin payload to create a marketplace program.</summary>
public sealed record CreateProgramRequest(
    Guid SpecialtyId,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description);

/// <summary>Admin payload to update a marketplace program (full replace of mutable fields).</summary>
public sealed record UpdateProgramRequest(
    Guid SpecialtyId,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description);
