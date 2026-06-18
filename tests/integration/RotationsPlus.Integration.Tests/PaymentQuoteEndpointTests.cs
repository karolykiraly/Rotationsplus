using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Payments;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// GET /api/programs/{id}/quote — the server-computed price quote. Verifies the deposit math against
/// the seeded non-open and open programs, the week-validation boundaries, the 404 for an unknown
/// program, and that a signed-in customer (not just staff) can fetch a quote.
/// </summary>
public class PaymentQuoteEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Seeded programs (RotationProgramConfiguration): the in-person one is non-open (deposit), the
    // tele-rotation is seeded open (charged in full).
    private static readonly Guid NonOpenProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001"); // 1500/wk, min 4, non-open
    private static readonly Guid OpenProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");    // 1000/wk, min 2, open

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
    public async Task Non_open_program_quotes_a_ten_percent_deposit()
    {
        var quote = await Client(RoleNames.Admin)
            .GetFromJsonAsync<RotationQuoteResponse>($"/api/programs/{NonOpenProgramId}/quote?weeks=4", JsonOptions);

        quote!.Currency.Should().Be("USD");
        quote.RetailAmountPerWeek.Should().Be(1500m);
        quote.Weeks.Should().Be(4);
        quote.TotalAmount.Should().Be(6000m);
        quote.DepositAmount.Should().Be(600m);
        quote.OutstandingAmount.Should().Be(5400m);
        quote.DepositPercent.Should().Be(10m);
        quote.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Open_program_quotes_the_full_amount()
    {
        var quote = await Client(RoleNames.Admin)
            .GetFromJsonAsync<RotationQuoteResponse>($"/api/programs/{OpenProgramId}/quote?weeks=4", JsonOptions);

        quote!.TotalAmount.Should().Be(4000m);
        quote.DepositAmount.Should().Be(4000m);
        quote.OutstandingAmount.Should().Be(0m);
        quote.DepositPercent.Should().Be(100m);
        quote.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task A_signed_in_student_can_fetch_a_quote()
    {
        var response = await Client(RoleNames.Student)
            .GetAsync($"/api/programs/{OpenProgramId}/quote?weeks=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<RotationQuoteResponse>(JsonOptions);
        quote!.TotalAmount.Should().Be(2000m);
    }

    [Fact]
    public async Task Weeks_below_the_program_minimum_is_rejected()
    {
        // The non-open program requires at least 4 weeks.
        var response = await Client(RoleNames.Admin).GetAsync($"/api/programs/{NonOpenProgramId}/quote?weeks=2");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]      // weeks omitted
    [InlineData("?weeks=0")]
    [InlineData("?weeks=-1")]
    [InlineData("?weeks=99999")]
    [InlineData("?weeks=abc")] // unparseable — must 400, not 500
    public async Task Invalid_week_counts_are_rejected(string query)
    {
        var response = await Client(RoleNames.Admin).GetAsync($"/api/programs/{OpenProgramId}/quote{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_program_returns_404()
    {
        var response = await Client(RoleNames.Admin)
            .GetAsync($"/api/programs/{Guid.NewGuid()}/quote?weeks=4");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
