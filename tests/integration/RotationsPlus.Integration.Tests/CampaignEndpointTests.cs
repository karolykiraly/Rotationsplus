using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// Admin email campaigns: POST /api/campaigns (compose a draft), GET (list/detail), POST .../send
/// (draft → queued + dispatched to the Worker). Verifies validation, the draft-only send guard, the
/// dispatch, and the AdminOnly boundary.
/// </summary>
public class CampaignEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private async Task<CampaignDetailResponse> CreateDraftAsync(HttpClient admin, EmailAudience audience = EmailAudience.AllStudents)
    {
        var response = await admin.PostAsJsonAsync("/api/campaigns",
            new CreateCampaignRequest("Spring rotations are open", "Book your spring rotation today.", audience), JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CampaignDetailResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Create_then_get_and_list_round_trips_a_draft()
    {
        var admin = Client(RoleNames.Admin);
        var created = await CreateDraftAsync(admin, EmailAudience.AllPreceptors);

        created.Status.Should().Be(CampaignStatus.Draft);
        created.Audience.Should().Be(EmailAudience.AllPreceptors);
        created.RecipientCount.Should().Be(0);

        var detail = await admin.GetFromJsonAsync<CampaignDetailResponse>($"/api/campaigns/{created.Id}", JsonOptions);
        detail!.Subject.Should().Be("Spring rotations are open");
        detail.Body.Should().Be("Book your spring rotation today.");

        var list = await admin.GetFromJsonAsync<List<CampaignSummaryResponse>>("/api/campaigns", JsonOptions);
        list!.Should().Contain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Sending_a_draft_queues_it_and_dispatches_to_the_worker()
    {
        var admin = Client(RoleNames.Admin);
        var created = await CreateDraftAsync(admin);

        var sent = await (await admin.PostAsync($"/api/campaigns/{created.Id}/send", null))
            .Content.ReadFromJsonAsync<CampaignDetailResponse>(JsonOptions);

        sent!.Status.Should().Be(CampaignStatus.Queued);
        factory.CampaignDispatcher.Dispatched.Should().Contain(created.Id);
    }

    [Fact]
    public async Task Sending_a_non_draft_campaign_is_rejected()
    {
        var admin = Client(RoleNames.Admin);
        var created = await CreateDraftAsync(admin);
        (await admin.PostAsync($"/api/campaigns/{created.Id}/send", null)).EnsureSuccessStatusCode(); // → Queued

        var second = await admin.PostAsync($"/api/campaigns/{created.Id}/send", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict); // can't re-send a queued campaign
    }

    [Fact]
    public async Task An_empty_subject_is_rejected()
    {
        var admin = Client(RoleNames.Admin);
        var response = await admin.PostAsJsonAsync("/api/campaigns",
            new CreateCampaignRequest("   ", "body", EmailAudience.AllStudents), JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_admin_staff_are_rejected_with_403()
    {
        var response = await Client(RoleNames.Sales).GetAsync("/api/campaigns");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_is_rejected_with_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/campaigns");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
