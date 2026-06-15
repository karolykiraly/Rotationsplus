namespace RotationsPlus.Common.Authorization;

/// <summary>
/// App-role names emitted in Entra tokens. Staff roles come from the workforce tenant;
/// customer roles from the External ID (CIAM) tenant. See Plan_Architecture.md §3.5.
/// </summary>
public static class RoleNames
{
    // Workforce (staff) roles.
    public const string Admin = "Admin";
    public const string Sales = "Sales";
    public const string Sdr = "SDR";
    public const string Institution = "Institution";
    public const string Coordinator = "Coordinator";

    // Customer roles.
    public const string Student = "Student";
    public const string Preceptor = "Preceptor";

    public static readonly string[] Staff = [Admin, Sales, Sdr, Institution, Coordinator];
    public static readonly string[] Customer = [Student, Preceptor];
}
