using System.Net;
using FluentAssertions;

namespace RotationsPlus.Integration.Tests;

public class HealthTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    [Fact]
    public async Task Alive_returns_200()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
