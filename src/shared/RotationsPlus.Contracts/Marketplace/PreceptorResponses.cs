namespace RotationsPlus.Contracts.Marketplace;

/// <summary>A preceptor as shown in the directory list and the admin Permission queue (which also shows
/// the phone + onboarding-call-scheduled flag).</summary>
public sealed record PreceptorSummaryResponse(
    Guid Id,
    string FullName,
    string Email,
    string PrimarySpecialtyName,
    string? City,
    string? State,
    string? MobilePhone,
    bool CallScheduled,
    PreceptorStatus Status);

/// <summary>Full detail for a single preceptor.</summary>
public sealed record PreceptorDetailResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    Guid PrimarySpecialtyId,
    string PrimarySpecialtyName,
    string? MedicalLicenseNumber,
    string? LicenseState,
    string? City,
    string? State,
    PreceptorStatus Status,
    string? Bio,
    DateTimeOffset? ReviewedAtUtc = null,
    string? RejectionReason = null);

/// <summary>Admin Permission screen batch save: the preceptors to activate (Activated checkbox) and the
/// preceptors to reject (Reject checkbox). Mirrors the legacy <c>updatePreceptorPermissions({ ids, ids_d })</c>.
/// Only Pending preceptors are affected; an id in both lists is rejected (400).</summary>
public sealed record SavePreceptorPermissionsRequest(
    IReadOnlyList<Guid> ActivateIds,
    IReadOnlyList<Guid> RejectIds);

/// <summary>Result of a Permission save: how many preceptors were activated / rejected.</summary>
public sealed record SavePreceptorPermissionsResponse(int Activated, int Rejected);
