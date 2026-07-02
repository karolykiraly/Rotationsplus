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

/// <summary>
/// Save payload for the student profile's <b>Education</b> tab (legacy <c>onSaveProfile3</c>). The tab
/// branches by academic track (IMS/IMG USMLE, D.O. COMLEX, Pre-med, Dental); the client sends the active
/// branch's fields and leaves the rest null. School + country are shared by the IMS/IMG + Dental branches.
/// </summary>
public sealed record UpdateStudentEducationRequest(
    string? MedicalSchool, string? MedicalSchoolCountry, DateOnly? GraduationDate,
    ExamStatus? UsmleStep1, string? UsmleScore1, int? UsmleAttempts1, DateOnly? UsmleDate1,
    ExamStatus? UsmleStep2, string? UsmleScore2, int? UsmleAttempts2, DateOnly? UsmleDate2,
    ExamStatus? UsmleStep3, string? UsmleScore3, int? UsmleAttempts3, DateOnly? UsmleDate3,
    bool? EcfmgCertified, bool? AppliedMatch,
    bool? ComlexLevel1Taken, bool? ComlexLevel1Passed,
    ExamStatus? ComlexLevel2, string? ComlexLevel2Score, int? ComlexLevel2Attempts, DateOnly? ComlexLevel2Date,
    ExamStatus? ComlexLevel3, string? ComlexLevel3Score, int? ComlexLevel3Attempts, DateOnly? ComlexLevel3Date,
    string? Undergrad, EducationYear? EducationYear, bool? IsAmsa, string? Association, bool? IsLeadership,
    bool? IsToefl, bool? IsIndbe);
