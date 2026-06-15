using Microsoft.Extensions.DependencyInjection;

namespace RotationsPlus.Common.Authorization;

/// <summary>Named authorization policies. Hierarchy/gating detail lives in the per-area plan docs.</summary>
public static class AuthorizationPolicies
{
    public const string StaffOnly = "StaffOnly";
    public const string AdminOnly = "AdminOnly";
    public const string CustomerOnly = "CustomerOnly";
}

public static class AuthorizationPolicyExtensions
{
    /// <summary>Registers the baseline role-based policies used across the API.</summary>
    public static IServiceCollection AddRotationsPlusAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.StaffOnly, policy => policy.RequireRole(RoleNames.Staff))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
            .AddPolicy(AuthorizationPolicies.CustomerOnly, policy => policy.RequireRole(RoleNames.Customer));

        return services;
    }
}
