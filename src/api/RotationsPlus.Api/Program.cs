using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using RotationsPlus.Api.Endpoints;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Dashboard;
using RotationsPlus.Api.Modules.Identity;
using RotationsPlus.Api.Modules.Marketplace;
using RotationsPlus.Api.Modules.Payments;
using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Api.Modules.Students;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Data;
using RotationsPlus.Common.Security;
using RotationsPlus.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// --- Authentication: two Entra directories (Plan_Architecture.md §3.5). Staff tokens come from the
//     workforce tenant (AzureAd), customers from the External ID / CIAM tenant (AzureAdCustomer).
//     The "Smart" policy scheme is the default: it peeks each token's issuer and forwards to the
//     matching real scheme, so one pipeline accepts both. Role-based policies (StaffOnly/CustomerOnly)
//     then gate by the roles each directory emits. ---
var customerTenantId = builder.Configuration["AzureAdCustomer:TenantId"];
var authentication = builder.Services
    .AddAuthentication(AuthenticationSchemes.Smart)
    .AddPolicyScheme(AuthenticationSchemes.Smart, AuthenticationSchemes.Smart, options =>
        options.ForwardDefaultSelector = context => AuthSchemeSelector.Select(context, customerTenantId));

authentication.AddMicrosoftIdentityWebApi(
    builder.Configuration.GetSection("AzureAd"), AuthenticationSchemes.Workforce);
authentication.AddMicrosoftIdentityWebApi(
    builder.Configuration.GetSection("AzureAdCustomer"), AuthenticationSchemes.Customer);

// Serialize enums as their string names (e.g. "InPerson") for a stable, readable API contract.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRotationsPlusAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<StaffProfileProvisioner>();

// --- Payments: the provider is behind IPaymentGateway. DEV/test run on a deterministic fake (no live
//     keys); the real Stripe adapter + Key Vault signing secret slot in at PROD cutover (Plan_Migration §3). ---
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

// --- Program images: hospital photos live in blob storage; the API mints short-lived read SAS URLs.
//     The connection string is injected from Key Vault on DEV/PROD (infra/bicep/main.bicep). When it's
//     absent (local dev / integration tests) a functional in-memory store stands in so upload + serve
//     still work end-to-end without Azure. ---
builder.Services.Configure<ProgramImageOptions>(builder.Configuration.GetSection(ProgramImageOptions.SectionName));
if (!string.IsNullOrWhiteSpace(builder.Configuration[$"{ProgramImageOptions.SectionName}:ConnectionString"]))
{
    builder.Services.AddSingleton<IProgramImageStore, AzureBlobProgramImageStore>();
}
else
{
    builder.Services.AddSingleton<IProgramImageStore, InMemoryProgramImageStore>();
}

// --- Data: single Postgres DB / single DbContext (connection name "rotationsdb"). ---
// The audit interceptor stamps audit columns + soft-deletes. HttpContextAccessor's backing store
// is a static AsyncLocal, so a directly-constructed instance still resolves the current request's
// principal — which keeps the interceptor a simple singleton regardless of DbContext pooling.
// It reuses HttpCurrentUser so claim-reading lives in exactly one place.
var auditInterceptor = new AuditSaveChangesInterceptor(
    new HttpCurrentUser(new HttpContextAccessor()), TimeProvider.System);
builder.AddNpgsqlDbContext<RotationsDbContext>(
    "rotationsdb",
    configureDbContextOptions: options => options.AddInterceptors(auditInterceptor));

// --- CORS for the SPA. Origins come from config (localhost in Development; the SWA host on DEV). ---
const string spaCorsPolicy = "spa";
builder.Services.AddCors(options => options.AddPolicy(spaCorsPolicy, policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors(spaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// The Stripe-CLI analog that lets the SPA complete the deposit round-trip against the fake gateway.
// Gated on non-Production so it exists on DEV (env "Development") and in the integration-test host (env
// "Testing") but NEVER on PREPROD/PROD — both of which run as ASPNETCORE_ENVIRONMENT=Production (see
// infra/bicep/main.bicep), where a real provider webhook drives fulfilment.
if (!app.Environment.IsProduction())
{
    app.MapPaymentDevEndpoints();
}

app.MapMeEndpoints();
app.MapCustomerMeEndpoints();
app.MapCustomerRotationEndpoints();
app.MapSpecialtyEndpoints();
app.MapProgramEndpoints();
app.MapProgramImageEndpoints();
app.MapPaymentEndpoints();
app.MapPaymentWebhookEndpoints();
app.MapPaymentRefundEndpoints();
app.MapPreceptorEndpoints();
app.MapRotationEndpoints();
app.MapStudentEndpoints();
app.MapDashboardEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
