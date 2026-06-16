namespace RotationsPlus.Common.Authorization;

/// <summary>
/// JWT-bearer authentication scheme names. The API trusts two Entra directories (see
/// Plan_Architecture.md §3.5): the <see cref="Workforce"/> tenant for staff and the External ID
/// (CIAM) tenant for customers. <see cref="Smart"/> is the default scheme — a policy scheme that
/// inspects the incoming token's issuer and forwards to the matching real scheme, so a single
/// request pipeline accepts both staff and customer tokens.
/// </summary>
public static class AuthenticationSchemes
{
    /// <summary>Staff (workforce tenant) JWT bearer. Equals JwtBearerDefaults.AuthenticationScheme.</summary>
    public const string Workforce = "Bearer";

    /// <summary>Customer (CIAM / External ID tenant) JWT bearer.</summary>
    public const string Customer = "CustomerBearer";

    /// <summary>Issuer-routing policy scheme; the API's default authenticate/challenge scheme.</summary>
    public const string Smart = "Smart";
}
