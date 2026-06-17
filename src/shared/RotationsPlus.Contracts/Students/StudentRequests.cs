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
