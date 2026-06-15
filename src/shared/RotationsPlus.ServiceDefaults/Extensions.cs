using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace RotationsPlus.ServiceDefaults;

/// <summary>
/// Shared service defaults (clone of the SkyLimit / Aspire pattern): OpenTelemetry,
/// health checks, service discovery, and standard HTTP resilience. Both rplus-api and
/// rplus-worker call <see cref="AddServiceDefaults{TBuilder}"/> at startup.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // Set by Azure Container Apps (from Bicep). Its presence switches on the Azure Monitor exporter.
    private const string AzureMonitorConnectionKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Resilience (retries, circuit breaker, timeouts) + service discovery on every HttpClient by default.
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var azureMonitorConnectionString = builder.Configuration[AzureMonitorConnectionKey];

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
            {
                logging.AddAzureMonitorLogExporter(o => o.ConnectionString = azureMonitorConnectionString);
            }
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // OTLP endpoint (used by the Aspire dashboard locally, or any OTel collector).
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Application Insights (DEV/PREPROD/PROD): the container sets the connection string; without
        // this exporter that string is never read and no app telemetry reaches App Insights.
        // The matching log exporter is wired in ConfigureOpenTelemetry's logging pipeline.
        var azureMonitorConnectionString = builder.Configuration[AzureMonitorConnectionKey];
        if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = azureMonitorConnectionString))
                .WithTracing(tracing => tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = azureMonitorConnectionString));
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Liveness: the app is up. Tagged "live" so /alive can filter to just this.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>Maps /health (all checks) and /alive (liveness only). Required by Container Apps probes.</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }
}
