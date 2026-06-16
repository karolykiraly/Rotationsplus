using Microsoft.Extensions.DependencyInjection;

namespace RotationsPlus.Common.Authorization;

/// <summary>Named authorization policies. Hierarchy/gating detail lives in the per-area plan docs.</summary>
public static class AuthorizationPolicies
{
    public const string StaffOnly = "StaffOnly";
    public const string AdminOnly = "AdminOnly";
    public const string CustomerOnly = "CustomerOnly";

    /// <summary>Any authenticated marketplace user — staff OR customer. For catalog reads
    /// (specialties, programs) that both staff and signed-in students/preceptors may browse.</summary>
    public const string MarketplaceViewer = "MarketplaceViewer";
}

public static class AuthorizationPolicyExtensions
{
    /// <summary>
    /// Registers the baseline role-based policies used across the API. The cross-directory boundary
    /// (staff vs customer) is enforced in two layers: (1) the "Smart" scheme routes each token to the
    /// workforce or CIAM validator by issuer, so only genuinely-issued tokens authenticate; (2) these
    /// RequireRole policies gate on directory-specific role names, which are kept disjoint across the
    /// two Entra directories (invariant pinned by RoleBoundaryTests). Pinning each policy to its
    /// authenticating scheme is the next hardening step, gated on the integration harness learning to
    /// forward the real schemes to the TestAuthHandler (see Docs/Plan_Testing.md).
    /// </summary>
    public static IServiceCollection AddRotationsPlusAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.StaffOnly, policy => policy.RequireRole(RoleNames.Staff))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
            .AddPolicy(AuthorizationPolicies.CustomerOnly, policy => policy.RequireRole(RoleNames.Customer))
            .AddPolicy(AuthorizationPolicies.MarketplaceViewer, policy => policy.RequireRole([.. RoleNames.Staff, .. RoleNames.Customer]));

        return services;
    }
}
