namespace RotationsPlus.Contracts.Rotations;

/// <summary>Admin payload to create a rotation. The student is chosen from the directory by
/// <c>StudentId</c>; the server snapshots their name/email/oid onto the rotation. <c>Weeks</c> is
/// derived from the date range server-side.</summary>
public sealed record CreateRotationRequest(
    Guid ProgramId,
    Guid StudentId,
    DateOnly StartDate,
    DateOnly EndDate,
    RotationStatus Status);

/// <summary>Admin payload to update a rotation (full replace of mutable fields).</summary>
public sealed record UpdateRotationRequest(
    Guid ProgramId,
    Guid StudentId,
    DateOnly StartDate,
    DateOnly EndDate,
    RotationStatus Status);
