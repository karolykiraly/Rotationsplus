using Hangfire;
using Hangfire.PostgreSql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Email;
using RotationsPlus.Common.Jobs;
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

// Domain data access for jobs: the same RotationsDbContext (application schema) the API uses, against
// the same Postgres DB. No audit interceptor here — jobs run outside an HTTP request, and the campaign
// job sets its own timestamps; the global soft-delete query filters still apply.
builder.AddNpgsqlDbContext<RotationsDbContext>("rotationsdb");

// Email: the fake sender until a real provider is wired at cutover (swapped by registration).
builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();

// The campaign-send job, resolved by Hangfire when the API enqueues ICampaignSendJob.
builder.Services.AddScoped<ICampaignSendJob, SendCampaignJob>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseHangfireDashboard("/admin/jobs", new DashboardOptions
{
    Authorization = [new DashboardEnvironmentAuthorizationFilter(app.Environment.IsDevelopment())]
});

// P1 recurring job (heartbeat). Real cron-replacement jobs are registered here as each module lands.
RecurringJob.AddOrUpdate<HeartbeatJob>("heartbeat", job => job.RunAsync(), Cron.Hourly);

app.Run();
