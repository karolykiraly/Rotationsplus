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

/// <summary>A rotation as shown to the signed-in student in their portal "My rotations" view.
/// Internal/admin fields (student identity, honorarium) are omitted — the student knows who they are;
/// the program's preceptor and the dates/status are what they track.</summary>
public sealed record CustomerRotationResponse(
    Guid Id,
    string SpecialtyName,
    ProgramType ProgramType,
    string? PreceptorName,
    DateOnly StartDate,
    DateOnly EndDate,
    int Weeks,
    RotationStatus Status);

/// <summary>Full detail for a single rotation. <c>StudentId</c> links to the directory record
/// (null only for legacy rows); the name/email/oid are the snapshot taken at write time.</summary>
public sealed record RotationDetailResponse(
    Guid Id,
    Guid ProgramId,
    string SpecialtyName,
    ProgramType ProgramType,
    string? PreceptorName,
    Guid? StudentId,
    string StudentName,
    string StudentEmail,
    string? StudentOid,
    DateOnly StartDate,
    DateOnly EndDate,
    int Weeks,
    RotationStatus Status);
