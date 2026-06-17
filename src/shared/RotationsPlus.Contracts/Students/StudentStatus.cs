namespace RotationsPlus.Contracts.Students;

/// <summary>
/// Student onboarding/lifecycle status. Clean replacement for the legacy <c>student.status</c>
/// string values (registered, member_profile_completed, member_activated, turned_into_contact).
/// The admin directory and the CRM lead-conversion hook key off this.
/// </summary>
public enum StudentStatus
{
    Registered,
    MemberProfileCompleted,
    MemberActivated,
    TurnedIntoContact
}
