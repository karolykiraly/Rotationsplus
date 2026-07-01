namespace RotationsPlus.Contracts.Students;

/// <summary>
/// Granular work-authorization / immigration status shown on the student profile's Personal Information
/// tab — the faithful port of the legacy <c>visa_status</c> option list (11 fixed values + a free-text
/// "Other"). This is the profile's editable field; the coarser <see cref="VisaStatus"/> stays as the
/// operational flag driving the needs-visa filter and dashboard until they are unified post-port.
///
/// <para><see cref="NeedVisaInterviewScheduled"/> pairs with a <c>VisaInterviewDate</c>;
/// <see cref="Other"/> pairs with a free-text override.</para>
/// </summary>
public enum ImmigrationStatus
{
    UsCitizen,
    UsPermanentResident,
    PermanentResidentPending,
    B1B2,
    F1,
    J1,
    H1B,
    H4,
    Esta,
    NeedVisaInterviewScheduled,
    NeedVisaNoInterview,
    Other
}
