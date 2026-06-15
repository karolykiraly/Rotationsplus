using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using RotationsPlus.Api.Endpoints;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Identity;
using RotationsPlus.Api.Modules.Marketplace;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Data;
using RotationsPlus.Common.Security;
using RotationsPlus.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// --- Authentication: workforce (staff) Entra tokens, validated for the rplus-api audience.
//     Customer (CIAM) scheme is added in a later phase; P1 proves the staff round-trip. ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

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
app.MapSpecialtyEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
