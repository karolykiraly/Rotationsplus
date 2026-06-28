using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Integration.Tests;

/// <summary>The anonymous public landing feed (/api/public/programs). It must be reachable WITHOUT any
/// token (the marketing landing is anonymous) and must expose only public-safe fields.</summary>
public class PublicCatalogEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Anonymous_visitor_can_read_the_public_program_feed()
    {
        // No auth header at all — a public marketing visitor.
        var response = await factory.CreateClient().GetAsync("/api/public/programs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var programs = await response.Content.ReadFromJsonAsync<List<PublicProgramResponse>>(JsonOptions);
        programs.Should().NotBeNull();
        programs!.Should().NotBeEmpty(); // the seeded open programs

        // Public-safe projection: every item carries marketing fields only (the record has no honorarium
        // / preceptor / description members at all — the public can't even request them).
        programs.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.SpecialtyName));
        programs.Should().Contain(p => p.SpecialtyName == "Internal Medicine");
        typeof(PublicProgramResponse).GetProperty("WeeklyHonorarium").Should().BeNull();
        typeof(PublicProgramResponse).GetProperty("PreceptorName").Should().BeNull();
    }
}
