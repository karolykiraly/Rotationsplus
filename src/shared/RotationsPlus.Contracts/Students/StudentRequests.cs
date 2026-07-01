namespace RotationsPlus.Contracts.Students;

/// <summary>Admin payload to create a student directory record. <c>VisaStatus</c> is optional;
/// <c>StudentOid</c> links the record to a CIAM account once the student signs in.</summary>
public sealed record CreateStudentRequest(
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

/// <summary>Admin payload to update a student (full replace of mutable fields).</summary>
public sealed record UpdateStudentRequest(
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

/// <summary>
/// Save payload for the student profile's <b>Personal Information</b> tab (legacy <c>onSaveProfile1</c>).
/// Email is deliberately absent — it is the CIAM/Entra-linked identity and is read-only on the profile
/// (same rationale as the omitted password). Optional fields left null clear the value.
/// </summary>
public sealed record UpdateStudentPersonalInfoRequest(
    string FirstName,
    string LastName,
    string? MobilePhone,
    AcademicStatus AcademicStatus,
    DateOnly? Birthdate,
    Gender? Gender,
    ImmigrationStatus? ImmigrationStatus,
    string? ImmigrationStatusOther,
    DateOnly? VisaInterviewDate,
    string? PassportIssuedCountry,
    string? PassportNumber,
    StudentIdType? SelectedIdType,
    string? IdNumber);
