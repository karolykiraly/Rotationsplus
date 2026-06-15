using RotationsPlus.Api.Modules.Identity;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Identity;

namespace RotationsPlus.Api.Endpoints;

/// <summary>
/// GET /api/me — the staff Entra login round-trip. The SPA acquires a workforce token and calls
/// this; the handler provisions/refreshes the caller's persisted <see cref="StaffProfile"/> and
/// returns the identity, proving end-to-end auth + DB read/write on DEV.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/me", async (
            ICurrentUser user,
            StaffProfileProvisioner provisioner,
            CancellationToken cancellationToken) =>
        {
            var roles = user.Roles;
            var isStaff = roles.Any(RoleNames.Staff.Contains);

            var profile = await provisioner.EnsureProvisionedAsync(cancellationToken);

            return Results.Ok(new MeResponse(
                ObjectId: user.ObjectId ?? string.Empty,
                Name: user.Name,
                Username: user.Username,
                Roles: roles,
                IsStaff: isStaff,
                ProfileId: profile.Id,
                LastSignInAtUtc: profile.LastSignInAtUtc));
        })
        .RequireAuthorization(AuthorizationPolicies.StaffOnly)
        .WithName("GetMe")
        .WithTags("Identity");

        return routes;
    }
}
