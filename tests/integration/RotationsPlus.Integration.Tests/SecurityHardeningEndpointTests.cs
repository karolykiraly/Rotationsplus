using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// PHASE 2e security hardening: rate limiting (SEC-2) on the anonymous webhook and the uniform
/// ProblemDetails error contract on a rejected request (SEC-3). The scheme-pinning (SEC-1) is
/// exercised implicitly by the whole authorization-matrix suite continuing to pass once each policy
/// is pinned to its authenticating scheme.
/// </summary>
public class SecurityHardeningEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    /// <summary>A factory whose webhook rate limit is tiny, so a short burst trips it deterministically.
    /// A wide window means the permits don't refill mid-test. Isolated host → its own limiter state.</summary>
    private HttpClient TightlyRateLimitedClient() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Webhook:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Webhook:WindowSeconds", "300");
        }).CreateClient();

    [Fact]
    public async Task Webhook_rate_limit_rejects_a_burst_with_429()
    {
        var client = TightlyRateLimitedClient();

        // Junk payload + signature → the endpoint would 400 (bad signature), but the limiter runs first.
        // With a permit limit of 2, the first two are admitted (and 400 on signature) and the third is
        // rejected by the limiter before it reaches the handler.
        async Task<HttpResponseMessage> Hit() =>
            await client.PostAsync("/api/webhooks/stripe", new StringContent("{}"));

        var first = await Hit();
        var second = await Hit();
        var third = await Hit();

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_rate_limited_response_uses_the_problem_details_contract()
    {
        var client = TightlyRateLimitedClient();

        await client.PostAsync("/api/webhooks/stripe", new StringContent("{}"));
        await client.PostAsync("/api/webhooks/stripe", new StringContent("{}"));
        var rejected = await client.PostAsync("/api/webhooks/stripe", new StringContent("{}"));

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        // SEC-3: a 429 carries an RFC 7807 ProblemDetails body and a Retry-After hint.
        rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        rejected.Headers.Should().ContainKey("Retry-After");
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemShape>();
        problem!.Title.Should().Be("Too many requests");
        problem.Status.Should().Be(429);
    }

    private sealed record ProblemShape(string? Title, int? Status, string? Detail);
}
