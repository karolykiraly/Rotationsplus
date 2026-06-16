using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Identity;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/customer/me — the customer (CIAM) round-trip. Verifies the CustomerOnly policy lets
/// Student/Preceptor through and reflects their role flags, while staff and anonymous callers are
/// rejected (the cross-directory boundary). The authz-matrix covers the full role grid; these add
/// the response-shape assertions.
/// </summary>
public class CustomerMeEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private HttpClient CustomerClient(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        client.DefaultRequestHeaders.Add("X-Test-Name", $"{role} Person");
        client.DefaultRequestHeaders.Add("X-Test-Username", $"{role.ToLowerInvariant()}@example.com");
        return client;
    }

    [Fact]
    public async Task Returns_identity_for_a_student()
    {
        var me = await CustomerClient(RoleNames.Student)
            .GetFromJsonAsync<CustomerMeResponse>("/api/customer/me");

        me.Should().NotBeNull();
        me!.ObjectId.Should().Be($"oid-{RoleNames.Student}");
        me.Roles.Should().Contain(RoleNames.Student);
        me.IsStudent.Should().BeTrue();
        me.IsPreceptor.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_identity_for_a_preceptor()
    {
        var me = await CustomerClient(RoleNames.Preceptor)
            .GetFromJsonAsync<CustomerMeResponse>("/api/customer/me");

        me.Should().NotBeNull();
        me!.IsPreceptor.Should().BeTrue();
        me.IsStudent.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_staff_with_403()
    {
        var response = await CustomerClient(RoleNames.Coordinator).GetAsync("/api/customer/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_anonymous_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/customer/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
