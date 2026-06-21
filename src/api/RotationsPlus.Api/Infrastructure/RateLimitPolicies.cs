namespace RotationsPlus.Api.Infrastructure;

/// <summary>
/// Named rate-limiting policies. Applied per-endpoint with <c>RequireRateLimiting</c>; endpoints
/// without one are unlimited. Limits are config-bound (<c>RateLimiting:*</c>) with generous defaults
/// so normal traffic — and the integration suite — never trips them; only abuse does.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>The anonymous provider webhook — partitioned per client IP (it carries no identity).
    /// The one unauthenticated write surface, so it's the primary flood target.</summary>
    public const string Webhook = "webhook";

    /// <summary>Authenticated money paths (open-deposit, self-booking) — partitioned per user (oid),
    /// so one abusive account can't spam intent/booking creation, while other users are unaffected.</summary>
    public const string Payments = "payments";
}
