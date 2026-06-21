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
    /// (staff vs customer) is enforced in three layers: (1) the "Smart" scheme routes each token to the
    /// workforce or CIAM validator by issuer, so only genuinely-issued tokens authenticate; (2) each
    /// policy is PINNED to its authenticating scheme(s) (workforce for staff/admin, customer for
    /// customer, both for the shared marketplace viewer) so a token validated by the wrong directory
    /// can't satisfy it even if a role name were ever shared; (3) these RequireRole policies gate on
    /// directory-specific role names, which are kept disjoint across the two Entra directories
    /// (invariant pinned by RoleBoundaryTests). The integration harness forwards the real schemes to
    /// the TestAuthHandler (see RotationsApiFactory), so the pinned schemes resolve in tests too.
    /// </summary>
    public static IServiceCollection AddRotationsPlusAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.StaffOnly, policy => policy
                .AddAuthenticationSchemes(AuthenticationSchemes.Workforce)
                .RequireRole(RoleNames.Staff))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
                .AddAuthenticationSchemes(AuthenticationSchemes.Workforce)
                .RequireRole(RoleNames.Admin))
            .AddPolicy(AuthorizationPolicies.CustomerOnly, policy => policy
                .AddAuthenticationSchemes(AuthenticationSchemes.Customer)
                .RequireRole(RoleNames.Customer))
            .AddPolicy(AuthorizationPolicies.MarketplaceViewer, policy => policy
                .AddAuthenticationSchemes(AuthenticationSchemes.Workforce, AuthenticationSchemes.Customer)
                .RequireRole([.. RoleNames.Staff, .. RoleNames.Customer]));

        return services;
    }
}
