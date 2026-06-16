namespace RotationsPlus.Contracts.Marketplace;

/// <summary>Admin payload to create a marketplace program. <c>PreceptorId</c> is optional — null
/// leaves the program unassigned.</summary>
public sealed record CreateProgramRequest(
    Guid SpecialtyId,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description,
    Guid? PreceptorId);

/// <summary>Admin payload to update a marketplace program (full replace of mutable fields).
/// <c>PreceptorId</c> null clears the assignment.</summary>
public sealed record UpdateProgramRequest(
    Guid SpecialtyId,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description,
    Guid? PreceptorId);
