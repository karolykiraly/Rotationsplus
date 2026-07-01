namespace RotationsPlus.Contracts.Students;

/// <summary>
/// A student's / preceptor's self-reported gender, as captured on the profile's Personal Information tab
/// (legacy <c>gender</c>: <c>male</c> / <c>female</c> / <c>none</c>). <c>NonBinary</c> is the legacy
/// <c>none</c> value. Optional on a record (null = not captured).
/// </summary>
public enum Gender
{
    Male,
    Female,
    NonBinary
}
