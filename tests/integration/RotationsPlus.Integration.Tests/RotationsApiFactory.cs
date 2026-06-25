using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Crm;
using RotationsPlus.Common.Authorization;
using Testcontainers.PostgreSql;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// Spins up a real PostgreSQL (Testcontainers) and boots the API with the header-driven
/// <see cref="TestAuthHandler"/> as the default auth scheme. Requires Docker (CI agents provide it).
/// </summary>
public sealed class RotationsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("rotationsplus_it")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Records campaign dispatches in place of the real Hangfire enqueue, so a test can assert a
    /// send was dispatched without standing up Hangfire storage.</summary>
    public RecordingCampaignDispatcher CampaignDispatcher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:rotationsdb", _postgres.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // Record campaign dispatches instead of enqueuing a real Hangfire job (the Worker isn't
            // running in tests). The Hangfire client itself is skipped in the Testing host.
            services.AddSingleton<ICampaignDispatcher>(CampaignDispatcher);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;

                // The production policies are scheme-pinned (workforce/customer JWT bearer). Point those
                // real scheme names at the header-driven TestAuthHandler so a scheme-pinned policy
                // authenticates the same X-Test-* headers in tests instead of falling through to the
                // unconfigured JwtBearer handler (which would 401 every pinned endpoint). The handler
                // reads roles from headers, so staff-vs-customer is still exercised per request.
                foreach (var name in new[] { AuthenticationSchemes.Workforce, AuthenticationSchemes.Customer })
                {
                    if (options.SchemeMap.TryGetValue(name, out var scheme))
                    {
                        scheme.HandlerType = typeof(TestAuthHandler);
                    }
                }
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Apply the real EF migrations to the throwaway container — this also exercises the
        // migration itself on every CI run (catches a broken migration before it reaches DEV).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        await db.Database.MigrateAsync();
    }

    // Explicit to avoid clashing with WebApplicationFactory's ValueTask DisposeAsync().
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
