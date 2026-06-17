namespace RotationsPlus.Contracts.Students;

/// <summary>
/// A student's work-authorization / visa situation. Clean replacement for the legacy free-text
/// <c>visa_status</c> strings. Optional on a record (null = unknown/not captured yet). Drives the
/// PAL-document and visa-interview-reminder flows in later slices.
/// </summary>
public enum VisaStatus
{
    CitizenOrGreenCard,
    ValidVisa,
    InterviewScheduled,
    NeedsVisaHelp
}
