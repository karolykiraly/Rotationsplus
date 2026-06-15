using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Identity;

namespace RotationsPlus.Api.Endpoints;

/// <summary>
/// GET /api/me — the P1 staff Entra login round-trip. The SPA acquires a workforce token,
/// calls this, and renders the identity to prove end-to-end auth on DEV.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/me", (ICurrentUser user) =>
        {
            var roles = user.Roles;
            var isStaff = roles.Any(RoleNames.Staff.Contains);

            return Results.Ok(new MeResponse(
                ObjectId: user.ObjectId ?? string.Empty,
                Name: user.Name,
                Username: user.Username,
                Roles: roles,
                IsStaff: isStaff));
        })
        .RequireAuthorization(AuthorizationPolicies.StaffOnly)
        .WithName("GetMe")
        .WithTags("Identity");

        return routes;
    }
}
