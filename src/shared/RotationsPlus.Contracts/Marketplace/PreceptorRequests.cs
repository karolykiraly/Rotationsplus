namespace RotationsPlus.Contracts.Marketplace;

/// <summary>Admin payload to create a preceptor directory record.</summary>
public sealed record CreatePreceptorRequest(
    string FirstName,
    string LastName,
    string Email,
    Guid PrimarySpecialtyId,
    string? MedicalLicenseNumber,
    string? LicenseState,
    string? City,
    string? State,
    PreceptorStatus Status,
    string? Bio);

/// <summary>Admin payload to update a preceptor (full replace of mutable fields).</summary>
public sealed record UpdatePreceptorRequest(
    string FirstName,
    string LastName,
    string Email,
    Guid PrimarySpecialtyId,
    string? MedicalLicenseNumber,
    string? LicenseState,
    string? City,
    string? State,
    PreceptorStatus Status,
    string? Bio);
