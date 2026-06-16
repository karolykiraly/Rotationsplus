using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;

namespace RotationsPlus.Api.Tests;

/// <summary>
/// Unit-tests the issuer-routing selector behind the "Smart" policy scheme. It must send CIAM
/// tokens to the customer scheme and everything else (staff tokens, junk, no token) to the
/// workforce scheme — which then does the real validation.
/// </summary>
public class AuthSchemeSelectorTests
{
    private const string CiamTenantId = "f963c59e-da79-40f4-a358-1cd77e78ddd0";
    private const string WorkforceTenantId = "36486bcb-8a3f-4499-b0fc-9a06f510ec0e";

    private static HttpContext WithBearer(string? token)
    {
        var context = new DefaultHttpContext();
        if (token is not null)
        {
            context.Request.Headers.Authorization = $"Bearer {token}";
        }

        return context;
    }

    /// <summary>Builds a structurally-valid but unsigned JWT carrying the given issuer.</summary>
    private static string UnsignedJwt(string issuer)
    {
        static string B64Url(string s) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64Url($"{{\"iss\":\"{issuer}\"}}");
        return $"{header}.{payload}.";
    }

    [Fact]
    public void Routes_ciam_issuer_to_the_customer_scheme()
    {
        var token = UnsignedJwt($"https://{CiamTenantId}.ciamlogin.com/{CiamTenantId}/v2.0");

        AuthSchemeSelector.Select(WithBearer(token), CiamTenantId)
            .Should().Be(AuthenticationSchemes.Customer);
    }

    [Fact]
    public void Routes_workforce_issuer_to_the_workforce_scheme()
    {
        var token = UnsignedJwt($"https://login.microsoftonline.com/{WorkforceTenantId}/v2.0");

        AuthSchemeSelector.Select(WithBearer(token), CiamTenantId)
            .Should().Be(AuthenticationSchemes.Workforce);
    }

    [Fact]
    public void Falls_back_to_workforce_when_no_authorization_header()
    {
        AuthSchemeSelector.Select(WithBearer(null), CiamTenantId)
            .Should().Be(AuthenticationSchemes.Workforce);
    }

    [Fact]
    public void Falls_back_to_workforce_for_a_non_bearer_header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";

        AuthSchemeSelector.Select(context, CiamTenantId)
            .Should().Be(AuthenticationSchemes.Workforce);
    }

    [Fact]
    public void Falls_back_to_workforce_for_a_malformed_token()
    {
        AuthSchemeSelector.Select(WithBearer("not-a-jwt"), CiamTenantId)
            .Should().Be(AuthenticationSchemes.Workforce);
    }

    // These two pass JsonWebTokenHandler.CanReadToken (right shape) but make ReadJsonWebToken THROW
    // (undecodable segments). Before the try/catch guard this surfaced as a 500 on the unauthenticated
    // path; it must fall back to the workforce scheme, which then rejects the token as a clean 401.
    [Theory]
    [InlineData("abc.def.ghi")]   // three readable-looking but non-base64url segments
    [InlineData("a.b.c.d.e")]     // five segments (JWE shape)
    public void Falls_back_to_workforce_when_token_is_structurally_tokenish_but_unparseable(string token)
    {
        AuthSchemeSelector.Select(WithBearer(token), CiamTenantId)
            .Should().Be(AuthenticationSchemes.Workforce);
    }

    [Fact]
    public void Falls_back_to_workforce_for_an_empty_token_after_the_bearer_prefix()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer ";

        AuthSchemeSelector.Select(context, CiamTenantId)
            .Should().Be(AuthenticationSchemes.Workforce);
    }

    [Fact]
    public void Matches_the_ciam_issuer_case_insensitively()
    {
        // Same routing must hold if the issuer carries the tenant id in a different case.
        var token = UnsignedJwt($"https://{CiamTenantId.ToUpperInvariant()}.ciamlogin.com/v2.0");

        AuthSchemeSelector.Select(WithBearer(token), CiamTenantId)
            .Should().Be(AuthenticationSchemes.Customer);
    }

    [Fact]
    public void Falls_back_to_workforce_when_customer_tenant_is_not_configured()
    {
        var token = UnsignedJwt($"https://{CiamTenantId}.ciamlogin.com/{CiamTenantId}/v2.0");

        AuthSchemeSelector.Select(WithBearer(token), customerTenantId: null)
            .Should().Be(AuthenticationSchemes.Workforce);
    }
}
