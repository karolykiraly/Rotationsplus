namespace RotationsPlus.Contracts.Rotations;

/// <summary>Admin payload to create a rotation (manual booking). <c>Weeks</c> is derived from the
/// date range server-side. <c>StudentOid</c> is optional until the CIAM account is linked.</summary>
public sealed record CreateRotationRequest(
    Guid ProgramId,
    string StudentName,
    string StudentEmail,
    string? StudentOid,
    DateOnly StartDate,
    DateOnly EndDate,
    RotationStatus Status);

/// <summary>Admin payload to update a rotation (full replace of mutable fields).</summary>
public sealed record UpdateRotationRequest(
    Guid ProgramId,
    string StudentName,
    string StudentEmail,
    string? StudentOid,
    DateOnly StartDate,
    DateOnly EndDate,
    RotationStatus Status);
