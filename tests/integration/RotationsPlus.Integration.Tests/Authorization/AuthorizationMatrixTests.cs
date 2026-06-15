using System.Net;
using FluentAssertions;

namespace RotationsPlus.Integration.Tests.Authorization;

/// <summary>
/// The authorization-matrix gate: for every endpoint in <see cref="ApiAuthorizationMatrix"/>, an
/// anonymous caller is 401, an allowed role is authorized through (NOT 401/403), and every other
/// role is 403. This asserts authorization only — the concrete success/validation/404 status of
/// each endpoint is covered by endpoint-specific tests, which keeps the matrix valid for
/// parameterized and write endpoints (where an authorized call may legitimately be 400/404).
/// Theory data is all-strings so xUnit serializes it cleanly into readable case names.
/// </summary>
public class AuthorizationMatrixTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    public static IEnumerable<object[]> AnonymousCases() =>
        ApiAuthorizationMatrix.Endpoints.Select(e => new object[] { e.Method, e.Path });

    public static IEnumerable<object[]> RoleCases() =>
        from endpoint in ApiAuthorizationMatrix.Endpoints
        from role in ApiAuthorizationMatrix.AllRoles
        select new object[] { endpoint.Method, endpoint.Path, string.Join(',', endpoint.AllowedRoles), role };

    [Theory]
    [MemberData(nameof(AnonymousCases))]
    public async Task Anonymous_caller_is_unauthorized(string method, string path)
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(RoleCases))]
    public async Task Role_access_matches_the_matrix(string method, string path, string allowedRolesCsv, string role)
    {
        var allowedRoles = allowedRolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Test-Oid", $"oid-{role}");
        request.Headers.Add("X-Test-Roles", role);

        var response = await client.SendAsync(request);

        if (allowedRoles.Contains(role))
        {
            // Authorized through — not rejected by authentication/authorization.
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
