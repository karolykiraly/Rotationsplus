using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Common;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

public class PreceptorEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private const string SeededJaneCarter = "dddddddd-0000-0000-0000-000000000001";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient StaffClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", "oid-staff");
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Coordinator);
        return client;
    }

    [Fact]
    public async Task List_returns_seeded_preceptors_with_specialty_names()
    {
        var preceptors = await StaffClient().GetFromJsonAsync<PagedResponse<PreceptorSummaryResponse>>("/api/preceptors", JsonOptions);

        preceptors.Should().NotBeNull();
        preceptors!.Items.Should().HaveCount(3); // Jane Carter, Nadia Khan (Pending — the approval-queue seed), Omar Reyes
        preceptors.TotalCount.Should().Be(3);
        preceptors.Items.Select(p => p.FullName).Should().Contain("Jane Carter");
        preceptors.Items.Select(p => p.PrimarySpecialtyName).Should().Contain("Internal Medicine");
        // Ordered by last name, then first.
        preceptors.Items.Select(p => p.FullName).Should().ContainInOrder("Jane Carter", "Nadia Khan", "Omar Reyes");
    }

    [Fact]
    public async Task Get_by_id_returns_full_detail()
    {
        var preceptor = await StaffClient().GetFromJsonAsync<PreceptorDetailResponse>($"/api/preceptors/{SeededJaneCarter}", JsonOptions);

        preceptor.Should().NotBeNull();
        preceptor!.FirstName.Should().Be("Jane");
        preceptor.LastName.Should().Be("Carter");
        preceptor.Email.Should().Be("jane.carter@example.com");
        preceptor.PrimarySpecialtyName.Should().Be("Internal Medicine");
        preceptor.City.Should().Be("Chicago");
        preceptor.Status.Should().Be(PreceptorStatus.MemberActivated);
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404()
    {
        var response = await StaffClient().GetAsync($"/api/preceptors/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_without_auth_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/preceptors");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
