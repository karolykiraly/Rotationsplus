namespace RotationsPlus.Contracts.Marketplace;

/// <summary>
/// Preceptor onboarding/lifecycle status. Clean replacement for the legacy <c>users.status</c>
/// string values (registered, pending, member_profile_completed, member_activated,
/// member_validated, member_signed). Login routing and the admin approval queue key off this.
/// <c>Rejected</c> is the terminal outcome of the admin approval queue (legacy had no distinct
/// value; rejection was implicit). Stored as a string, so appending here is migration-safe.
/// </summary>
public enum PreceptorStatus
{
    Registered,
    Pending,
    MemberProfileCompleted,
    MemberActivated,
    MemberValidated,
    MemberSigned,
    Rejected
}
