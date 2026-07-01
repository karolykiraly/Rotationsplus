namespace RotationsPlus.Contracts.Students;

/// <summary>
/// Which identity document a D.O. student provides in place of a passport, on the profile's Personal
/// Information tab (legacy <c>selected_id</c>: "Driving license" / "Passport"). Only captured for the
/// D.O. academic track; null otherwise.
/// </summary>
public enum StudentIdType
{
    DrivingLicense,
    Passport
}
