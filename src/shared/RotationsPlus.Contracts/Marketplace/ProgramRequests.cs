namespace RotationsPlus.Contracts.Marketplace;

/// <summary>Admin payload to create a marketplace program. <c>PreceptorId</c> is optional — null
/// leaves the program unassigned. <c>IsOpen</c> marks an instant-approval program (charged in full at
/// checkout vs. a deposit) and is optional, defaulting to a non-open (deposit) program.</summary>
public sealed record CreateProgramRequest(
    Guid SpecialtyId,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description,
    Guid? PreceptorId,
    bool IsOpen = false);

/// <summary>Admin payload to update a marketplace program (full replace of mutable fields).
/// <c>PreceptorId</c> null clears the assignment. <c>IsOpen</c> defaults to non-open.</summary>
public sealed record UpdateProgramRequest(
    Guid SpecialtyId,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal WeeklyHonorarium,
    string? Description,
    Guid? PreceptorId,
    bool IsOpen = false);
