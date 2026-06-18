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

app.MapMeEndpoints();
app.MapCustomerMeEndpoints();
app.MapCustomerRotationEndpoints();
app.MapSpecialtyEndpoints();
app.MapProgramEndpoints();
app.MapPaymentEndpoints();
app.MapPreceptorEndpoints();
app.MapRotationEndpoints();
app.MapStudentEndpoints();
app.MapDashboardEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
