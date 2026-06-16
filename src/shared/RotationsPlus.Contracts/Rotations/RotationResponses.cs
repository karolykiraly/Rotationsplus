using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Contracts.Rotations;

/// <summary>A rotation as shown in the admin list (program + preceptor flattened for the table).</summary>
public sealed record RotationSummaryResponse(
    Guid Id,
    string StudentName,
    string StudentEmail,
    string SpecialtyName,
    ProgramType ProgramType,
    string? PreceptorName,
    DateOnly StartDate,
    DateOnly EndDate,
    int Weeks,
    RotationStatus Status);

/// <summary>Full detail for a single rotation.</summary>
public sealed record RotationDetailResponse(
    Guid Id,
    Guid ProgramId,
    string SpecialtyName,
    ProgramType ProgramType,
    string? PreceptorName,
    string StudentName,
    string StudentEmail,
    string? StudentOid,
    DateOnly StartDate,
    DateOnly EndDate,
    int Weeks,
    RotationStatus Status);
