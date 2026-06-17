namespace RotationsPlus.Contracts.Students;

/// <summary>A student as shown in the admin directory list.</summary>
public sealed record StudentSummaryResponse(
    Guid Id,
    string FullName,
    string Email,
    string? MobilePhone,
    AcademicStatus AcademicStatus,
    VisaStatus? VisaStatus,
    string? City,
    string? State,
    StudentStatus Status);

/// <summary>Full detail for a single student.</summary>
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
    string? StudentOid);
