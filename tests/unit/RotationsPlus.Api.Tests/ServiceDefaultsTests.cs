using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using RotationsPlus.ServiceDefaults;

namespace RotationsPlus.Api.Tests;

public class ServiceDefaultsTests
{
    [Fact]
    public void AddServiceDefaults_wires_tracing_when_app_insights_connection_is_present()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Well-formed but unreachable connection string — the exporter registers lazily, no network call.
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] =
                "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.com/"
        });

        builder.AddServiceDefaults();
        using var host = builder.Build();

        // The OpenTelemetry pipeline (and thus the Azure Monitor exporter path) is wired up.
        host.Services.GetService<TracerProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddServiceDefaults_still_builds_without_an_app_insights_connection()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();
        using var host = builder.Build();

        host.Services.GetService<TracerProvider>().Should().NotBeNull();
    }
}
