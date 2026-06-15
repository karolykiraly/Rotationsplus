using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RotationsPlus.Common.Authorization;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// Header-driven test authentication (SkyLimit pattern). Tests set:
/// X-Test-Oid (required), X-Test-Roles (comma-separated), X-Test-Name, X-Test-Username.
/// No header ⇒ unauthenticated (401).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Oid", out var oid))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimNames.ObjectId, oid.ToString()),
            new(ClaimNames.Name, Request.Headers["X-Test-Name"].FirstOrDefault() ?? "Test User"),
            new(ClaimNames.PreferredUsername,
                Request.Headers["X-Test-Username"].FirstOrDefault() ?? "test@rotationsplus.org")
        };

        var roles = Request.Headers["X-Test-Roles"].FirstOrDefault() ?? string.Empty;
        foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimNames.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
