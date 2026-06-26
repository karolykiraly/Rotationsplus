namespace RotationsPlus.Contracts.Marketplace;

/// <summary>A preceptor as shown in the directory list.</summary>
public sealed record PreceptorSummaryResponse(
    Guid Id,
    string FullName,
    string Email,
    string PrimarySpecialtyName,
    string? City,
    string? State,
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

/// <summary>Admin rejection of a preceptor in the approval queue — carries the required reason.</summary>
public sealed record RejectPreceptorRequest(string Reason);
