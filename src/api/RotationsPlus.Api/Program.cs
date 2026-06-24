using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Identity.Web;
using RotationsPlus.Api.Endpoints;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Dashboard;
using RotationsPlus.Api.Modules.Documents;
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

// --- Uniform error contract: unhandled exceptions become an RFC 7807 ProblemDetails (instead of an
//     empty/HTML 500), and the exception handler is the one central sink for unexpected faults. In
//     Production no stack trace is emitted; the SPA gets a consistent JSON shape. ---
builder.Services.AddProblemDetails();

// --- Rate limiting (PHASE 2e / SEC-2): protect the unauthenticated webhook (per-IP) and the
//     authenticated money paths (per-user). Limits are config-bound with generous defaults so normal
//     traffic and the integration suite never trip them; a rejected request gets 429 + Retry-After. ---
builder.Services.AddRateLimiter(options =>
{
    var config = builder.Configuration;
    var webhookPermit = config.GetValue("RateLimiting:Webhook:PermitLimit", 240);
    var webhookWindow = TimeSpan.FromSeconds(config.GetValue("RateLimiting:Webhook:WindowSeconds", 60));
    var paymentsPermit = config.GetValue("RateLimiting:Payments:PermitLimit", 120);
    var paymentsWindow = TimeSpan.FromSeconds(config.GetValue("RateLimiting:Payments:WindowSeconds", 60));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        // Surface a retry hint and a ProblemDetails body so a 429 matches the rest of the error contract.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many requests",
                detail: "Rate limit exceeded. Please retry later.")
            .ExecuteAsync(context.HttpContext);
    };

    // Anonymous webhook → partition by client IP (no identity to key on).
    options.AddPolicy(RateLimitPolicies.Webhook, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = webhookPermit, Window = webhookWindow, QueueLimit = 0 }));

    // Authenticated money paths → partition by user oid (fall back to IP if somehow unauthenticated).
    options.AddPolicy(RateLimitPolicies.Payments, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimNames.ObjectId)
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = paymentsPermit, Window = paymentsWindow, QueueLimit = 0 }));
});

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

// --- Document files: student-uploaded rotation documents. Same Blob account as images (separate
//     `documents` container); same in-memory fallback when no connection string is configured. ---
builder.Services.Configure<DocumentFileOptions>(builder.Configuration.GetSection(DocumentFileOptions.SectionName));
if (!string.IsNullOrWhiteSpace(builder.Configuration[$"{DocumentFileOptions.SectionName}:ConnectionString"]))
{
    builder.Services.AddSingleton<IDocumentFileStore, AzureBlobDocumentFileStore>();
}
else
{
    builder.Services.AddSingleton<IDocumentFileStore, InMemoryDocumentFileStore>();
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

// Central fault sink → ProblemDetails. First so it wraps everything downstream.
app.UseExceptionHandler();

app.UseCors(spaCorsPolicy);
app.UseAuthentication();
// After authentication so the per-user (oid) payments partition can read the principal.
app.UseRateLimiter();
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
app.MapCustomerDocumentEndpoints();
app.MapAdminDocumentEndpoints();
app.MapProgramDocumentConfigEndpoints();
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
app.MapDashboardTodosEndpoints();
app.MapDashboardRevenueEndpoints();
app.MapDashboardReportsEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
