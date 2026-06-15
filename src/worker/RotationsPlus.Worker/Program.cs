using Hangfire;
using Hangfire.PostgreSql;
using RotationsPlus.ServiceDefaults;
using RotationsPlus.Worker.Infrastructure;
using RotationsPlus.Worker.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Hangfire uses the same Postgres database (its own schema) for job storage.
var connectionString = builder.Configuration.GetConnectionString("rotationsdb")
    ?? throw new InvalidOperationException(
        "Connection string 'rotationsdb' is required for Hangfire storage. " +
        "Set ConnectionStrings__rotationsdb (Key Vault on Azure; appsettings.Development.json locally).");

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseHangfireDashboard("/admin/jobs", new DashboardOptions
{
    Authorization = [new DashboardEnvironmentAuthorizationFilter(app.Environment.IsDevelopment())]
});

// P1 recurring job (heartbeat). Real cron-replacement jobs are registered here as each module lands.
RecurringJob.AddOrUpdate<HeartbeatJob>("heartbeat", job => job.RunAsync(), Cron.Hourly);

app.Run();
