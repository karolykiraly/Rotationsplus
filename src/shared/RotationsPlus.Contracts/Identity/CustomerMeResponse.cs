namespace RotationsPlus.Contracts.Identity;

/// <summary>
/// Shape returned by GET /api/customer/me — the authenticated customer (Student / Preceptor)
/// identity straight from the CIAM token. Unlike the staff <see cref="MeResponse"/>, no profile is
/// provisioned here; customer-record creation belongs to the student/preceptor onboarding slices.
/// </summary>
public sealed record CustomerMeResponse(
    string ObjectId,
    string? Name,
    string? Username,
    IReadOnlyList<string> Roles,
    bool IsStudent,
    bool IsPreceptor);
