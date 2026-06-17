using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/dashboard — the admin hub aggregate. Verifies the domain totals, the by-status pipeline,
/// the upcoming-starts window (ordered, today-onward), and the AdminOnly boundary.
/// </summary>
public class DashboardEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly Guid InternalMedicineProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid SamRiveraStudentId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    [Fact]
    public async Task Returns_domain_totals_and_a_consistent_status_pipeline()
    {
        var admin = Client(RoleNames.Admin);

        var dash = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", JsonOptions);

        // Seed baselines (use >= so the suite stays robust if other tests add rows to this class's DB).
        dash!.Students.Should().BeGreaterThanOrEqualTo(2);
        dash.Programs.Should().BeGreaterThanOrEqualTo(4);
        dash.Preceptors.Should().BeGreaterThanOrEqualTo(2);
        dash.Specialties.Should().BeGreaterThanOrEqualTo(15);
        dash.Rotations.Should().BeGreaterThanOrEqualTo(1);

        // The pipeline sums to the rotation total, and the seeded Active rotation is represented.
        dash.RotationsByStatus.Sum(s => s.Count).Should().Be(dash.Rotations);
        dash.RotationsByStatus.Should().Contain(s => s.Status == RotationStatus.Active);
    }

    [Fact]
    public async Task Upcoming_starts_lists_future_rotations_in_start_order()
    {
        var admin = Client(RoleNames.Admin);

        // Two far-future rotations (always "upcoming" regardless of the real clock), out of order.
        var later = await CreateRotationAsync(admin, new DateOnly(2099, 5, 1), new DateOnly(2099, 6, 1));
        var sooner = await CreateRotationAsync(admin, new DateOnly(2099, 1, 5), new DateOnly(2099, 2, 2));

        var dash = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", JsonOptions);

        var ids = dash!.UpcomingStarts.Select(u => u.Id).ToList();
        ids.Should().Contain(new[] { sooner, later });
        // The sooner start appears before the later one.
        ids.IndexOf(sooner).Should().BeLessThan(ids.IndexOf(later));
        // Ascending by start date overall.
        dash.UpcomingStarts.Select(u => u.StartDate).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Non_admin_staff_are_rejected_with_403()
    {
        var response = await Client(RoleNames.Sales).GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, DateOnly start, DateOnly end)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(InternalMedicineProgramId, SamRiveraStudentId, start, end, RotationStatus.NotStarted), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }
}
