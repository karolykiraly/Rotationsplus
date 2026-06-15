using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using RotationsPlus.Api.Endpoints;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
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
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// --- Data: single Postgres DB / single DbContext (connection name "rotationsdb"). ---
builder.AddNpgsqlDbContext<RotationsDbContext>("rotationsdb");

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

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
