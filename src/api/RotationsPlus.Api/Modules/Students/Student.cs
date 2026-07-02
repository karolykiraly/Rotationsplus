using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Api.Modules.Students;

/// <summary>
/// A student directory record — the demand side of the marketplace (the customer who books rotations).
/// This first slice models the identity + academic/visa classification core and the lifecycle status;
/// the deep profile (exam scores, documents, payments, favorites) and the rotation FK arrive in later
/// slices (see Plan_Student.md). <see cref="StudentOid"/> links the record to a CIAM account once the
/// student signs in to the portal.
/// </summary>
public sealed class Student : AuditableEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? MobilePhone { get; set; }

    public AcademicStatus AcademicStatus { get; set; }
    public VisaStatus? VisaStatus { get; set; }

    public string? MedicalSchool { get; set; }
    public string? MedicalSchoolCountry { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    // ---- Profile → Personal Information tab (legacy StudentProfile.js tab 0) ----
    // All optional: the directory record is created with the identity core; the profile fills these in.

    /// <summary>Date of birth (legacy <c>birthdate</c>).</summary>
    public DateOnly? Birthdate { get; set; }

    public Gender? Gender { get; set; }

    /// <summary>Granular immigration/visa status (legacy <c>visa_status</c> option list) shown on the
    /// profile. Distinct from the coarse <see cref="VisaStatus"/> that drives the needs-visa filter.</summary>
    public ImmigrationStatus? ImmigrationStatus { get; set; }

    /// <summary>Free-text override when <see cref="ImmigrationStatus"/> is <c>Other</c>.</summary>
    public string? ImmigrationStatusOther { get; set; }

    /// <summary>Scheduled visa-interview date (legacy <c>visa_interview_date</c>), captured when the
    /// immigration status is "need a visa, interview scheduled".</summary>
    public DateOnly? VisaInterviewDate { get; set; }

    /// <summary>Country that issued the passport (legacy <c>passport_issued_country</c>).</summary>
    public string? PassportIssuedCountry { get; set; }

    /// <summary>Passport number (legacy <c>passport</c>).</summary>
    public string? PassportNumber { get; set; }

    /// <summary>Which ID a D.O. student provides instead of a passport (legacy <c>selected_id</c>).</summary>
    public StudentIdType? SelectedIdType { get; set; }

    /// <summary>ID number for the <see cref="SelectedIdType"/> (legacy <c>id_number</c>, D.O. only).</summary>
    public string? IdNumber { get; set; }

    /// <summary>Blob name of the uploaded profile photo (legacy <c>avatar</c>); the upload/serve path is
    /// its own slice. Null until a photo is uploaded.</summary>
    public string? AvatarBlobName { get; set; }

    // ---- Profile → Needs tab (legacy StudentProfile.js tab 1) ----
    // Selections stored as titles (not the legacy positional numeric ids, which are ambiguous — dental id 1
    // ≠ non-dental id 1). Postgres text[] via Npgsql; null until the student fills the tab.

    /// <summary>Specialty interests chosen in the grid (legacy <c>interests</c>, stored as titles).</summary>
    public List<string>? Interests { get; set; }

    /// <summary>The single specialty picked from the "or add from the list" dropdown (legacy
    /// <c>specialty</c>).</summary>
    public string? PreferredSpecialty { get; set; }

    /// <summary>Preferred rotation locations (legacy <c>specialty_locations</c>, stored as city titles).</summary>
    public List<string>? SpecialtyLocations { get; set; }

    /// <summary>Free-text location when "Other" is chosen (legacy <c>custom_specialty_location</c>).</summary>
    public string? CustomSpecialtyLocation { get; set; }

    /// <summary>What matters most when finding a rotation (legacy <c>importants</c>, a CSV in Strapi;
    /// stored here as a clean string list). Hidden for the dental track.</summary>
    public List<string>? Importants { get; set; }

    // ---- Profile → Education tab (legacy StudentProfile.js tab 2 / onSaveProfile3) ----
    // Branches by academic track; all optional. MedicalSchool/MedicalSchoolCountry (above) are reused for
    // the IMS/IMG + Dental branches. Scores are strings (Step 1 allows "Pass"/"Fail" alongside 180–300).

    /// <summary>Graduation date (legacy <c>graduation_date</c>) — IMS/IMG, Dental, Pre-med.</summary>
    public DateOnly? GraduationDate { get; set; }

    // USMLE (IMS/IMG): step status + score/attempts (when Taken) + scheduled date (when WillTake).
    public ExamStatus? UsmleStep1 { get; set; }
    public string? UsmleScore1 { get; set; }
    public int? UsmleAttempts1 { get; set; }
    public DateOnly? UsmleDate1 { get; set; }
    public ExamStatus? UsmleStep2 { get; set; }
    public string? UsmleScore2 { get; set; }
    public int? UsmleAttempts2 { get; set; }
    public DateOnly? UsmleDate2 { get; set; }
    public ExamStatus? UsmleStep3 { get; set; }
    public string? UsmleScore3 { get; set; }
    public int? UsmleAttempts3 { get; set; }
    public DateOnly? UsmleDate3 { get; set; }

    /// <summary>ECFMG certified (legacy <c>ecfmg_certified</c>) — IMS/IMG.</summary>
    public bool? EcfmgCertified { get; set; }

    /// <summary>Applied to the MATCH before (legacy <c>applied_match</c>) — IMS/IMG + Dental.</summary>
    public bool? AppliedMatch { get; set; }

    // COMLEX (D.O.): Level 1 is a simple taken(yes/no) + passed; Levels 2CE/3 mirror the USMLE shape.
    public bool? ComlexLevel1Taken { get; set; }
    public bool? ComlexLevel1Passed { get; set; }
    public ExamStatus? ComlexLevel2 { get; set; }
    public string? ComlexLevel2Score { get; set; }
    public int? ComlexLevel2Attempts { get; set; }
    public DateOnly? ComlexLevel2Date { get; set; }
    public ExamStatus? ComlexLevel3 { get; set; }
    public string? ComlexLevel3Score { get; set; }
    public int? ComlexLevel3Attempts { get; set; }
    public DateOnly? ComlexLevel3Date { get; set; }

    // Pre-med.
    public string? Undergrad { get; set; }
    public EducationYear? EducationYear { get; set; }
    public bool? IsAmsa { get; set; }
    public string? Association { get; set; }
    public bool? IsLeadership { get; set; }

    // Dental.
    public bool? IsToefl { get; set; }
    public bool? IsIndbe { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Registered;

    /// <summary>CIAM object id, set once the student signs in to the portal (null until linked).</summary>
    public string? StudentOid { get; set; }
}
