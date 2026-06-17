namespace RotationsPlus.Contracts.Students;

/// <summary>
/// A student's academic track. Clean replacement for the legacy free-text <c>academic_status</c>
/// strings (US Pre-med, International Medical Student/Graduate, Physician/Nurse Practitioner Student,
/// plus the MD/DO/Dental tracks from the public signup). Drives specialty-list and profile-wizard
/// branching in later slices; here it classifies the directory record.
/// </summary>
public enum AcademicStatus
{
    UsPreMed,
    MdStudent,
    DoStudent,
    DentalStudent,
    InternationalMedicalStudent,
    InternationalMedicalGraduate,
    PhysicianAssistantStudent,
    NursePractitionerStudent
}
