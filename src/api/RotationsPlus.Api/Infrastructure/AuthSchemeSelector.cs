using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using RotationsPlus.Common.Authorization;

namespace RotationsPlus.Api.Infrastructure;

/// <summary>
/// Routes an incoming bearer token to the right JWT-bearer scheme by peeking (NOT validating) its
/// issuer. Tokens minted by the CIAM (External ID) tenant go to <see cref="AuthenticationSchemes.Customer"/>;
/// everything else (including malformed/absent tokens) falls back to <see cref="AuthenticationSchemes.Workforce"/>,
/// whose validation then accepts or rejects it. This is the <c>ForwardDefaultSelector</c> for the
/// "Smart" policy scheme, so one pipeline accepts both staff and customer tokens.
/// </summary>
public static class AuthSchemeSelector
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Returns the scheme name to forward to. <paramref name="customerTenantId"/> is the CIAM
    /// tenant id; a token whose issuer contains it is treated as a customer token. Reading the token
    /// here is unauthenticated — the selected scheme still fully validates signature/issuer/audience.
    /// </summary>
    public static string Select(HttpContext context, string? customerTenantId)
    {
        if (string.IsNullOrEmpty(customerTenantId))
        {
            return AuthenticationSchemes.Workforce;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) ||
            !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationSchemes.Workforce;
        }

        var token = header[BearerPrefix.Length..].Trim();
        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return AuthenticationSchemes.Workforce;
        }

        // CanReadToken is only a shallow structural check — ReadJsonWebToken still throws on a
        // token that is the right shape but has undecodable segments (a client-supplied value).
        // This runs before authentication on every request, so fail closed: any parse failure
        // routes to the workforce scheme, which then rejects the token cleanly (401) rather than
        // letting the exception surface as a 500.
        string issuer;
        try
        {
            issuer = handler.ReadJsonWebToken(token).Issuer;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            return AuthenticationSchemes.Workforce;
        }

        return issuer.Contains(customerTenantId, StringComparison.OrdinalIgnoreCase)
            ? AuthenticationSchemes.Customer
            : AuthenticationSchemes.Workforce;
    }
}
