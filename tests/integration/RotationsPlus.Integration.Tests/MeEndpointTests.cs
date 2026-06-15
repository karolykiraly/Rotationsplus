using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Identity;

namespace RotationsPlus.Integration.Tests;

public class MeEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    [Fact]
    public async Task Get_me_without_auth_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_me_as_staff_returns_identity()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-123");
        client.DefaultRequestHeaders.Add("X-Test-Name", "Jane Admin");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Admin);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");

        me.Should().NotBeNull();
        me!.ObjectId.Should().Be("oid-123");
        me.IsStaff.Should().BeTrue();
        me.Roles.Should().Contain(RoleNames.Admin);
    }

    [Fact]
    public async Task Get_me_as_customer_is_forbidden_by_StaffOnly_policy()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-999");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
