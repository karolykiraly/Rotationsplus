using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:rotationsdb", _postgres.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    // Explicit to avoid clashing with WebApplicationFactory's ValueTask DisposeAsync().
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
