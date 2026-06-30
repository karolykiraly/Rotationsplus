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

/// <summary>Admin payload to set the dashboard Upcoming-Starts row flags (legacy
/// <c>documents_approved</c> / <c>preceptor_confirmed</c>). Both are sent each time — the row checkboxes
/// reflect the current values, so the request is the full desired state of the two flags.</summary>
public sealed record RotationConfirmationsRequest(
    bool DocumentsApproved,
    bool PreceptorConfirmed);

/// <summary>Customer payload to self-book a rotation: the chosen program, a start date, and a duration in
/// <c>Weeks</c> (≥ the program minimum). The server resolves the student from the caller's CIAM oid,
/// computes the end date and price, and creates the booking as <c>Pending</c> (a student can't
/// self-approve — approval follows the deposit).</summary>
public sealed record CustomerBookingRequest(
    Guid ProgramId,
    DateOnly StartDate,
    int Weeks);
