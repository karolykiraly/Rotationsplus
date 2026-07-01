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

/// <summary>
/// Save payload for the student profile's <b>Needs</b> tab (legacy <c>onSaveProfile2</c>): specialty
/// interests, a single "add from the list" specialty, preferred locations (+ a free-text "Other"), and
/// the "what matters most" priorities. Selections are titles. <c>CustomSpecialtyLocation</c> is required
/// when the locations include "Other". The priorities list is hidden for the dental track.
/// </summary>
public sealed record UpdateStudentNeedsRequest(
    IReadOnlyList<string>? Interests,
    string? PreferredSpecialty,
    IReadOnlyList<string>? SpecialtyLocations,
    string? CustomSpecialtyLocation,
    IReadOnlyList<string>? Importants);
