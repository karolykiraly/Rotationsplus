namespace RotationsPlus.Contracts.Students;

/// <summary>
/// A student as shown in the admin directory list (the Contacts hub → Students tab). Carries the
/// production "achievements" rollups the tab renders (<c>DollarsSpent</c>, <c>OutstandingPayments</c>,
/// <c>OutstandingDocuments</c>, <c>WeeksPurchased</c>) alongside the identity core. The rollups are
/// computed server-side from the rewrite's own money/document model — production reads denormalized
/// Strapi fields we can't see, so these use model-native, documented definitions (see
/// <c>StudentEndpoints</c>): all money is derived from <b>succeeded</b> payments, so the numbers stay
/// consistent with the rest of the system's pricing/payment logic. Form pickers reuse this DTO with the
/// rollups defaulted to zero.
/// </summary>
public sealed record StudentSummaryResponse(
    Guid Id,
    string FullName,
    string Email,
    string? MobilePhone,
    AcademicStatus AcademicStatus,
    VisaStatus? VisaStatus,
    string? City,
    string? State,
    StudentStatus Status,
    decimal DollarsSpent,
    decimal OutstandingPayments,
    int OutstandingDocuments,
    int WeeksPurchased);

/// <summary>Full detail for a single student — the profile screen's load payload. Carries the identity/
/// academic core plus the Personal Information tab fields (birthdate, gender, granular immigration status,
/// passport/ID, avatar). Later profile tabs (Needs, Education, Sales…) extend this as they are built.</summary>
public sealed record StudentDetailResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? MobilePhone,
    AcademicStatus AcademicStatus,
    VisaStatus? VisaStatus,
    string? MedicalSchool,
    string? MedicalSchoolCountry,
    string? City,
    string? State,
    StudentStatus Status,
    string? StudentOid,
    // ---- Personal Information tab ----
    DateOnly? Birthdate,
    Gender? Gender,
    ImmigrationStatus? ImmigrationStatus,
    string? ImmigrationStatusOther,
    DateOnly? VisaInterviewDate,
    string? PassportIssuedCountry,
    string? PassportNumber,
    StudentIdType? SelectedIdType,
    string? IdNumber,
    string? AvatarBlobName,
    // ---- Needs tab ----
    IReadOnlyList<string>? Interests,
    string? PreferredSpecialty,
    IReadOnlyList<string>? SpecialtyLocations,
    string? CustomSpecialtyLocation,
    IReadOnlyList<string>? Importants);
