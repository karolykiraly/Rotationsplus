using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Identity;

namespace RotationsPlus.Api.Endpoints;

/// <summary>
/// GET /api/customer/me — the customer (CIAM) login round-trip, mirroring the staff
/// <see cref="MeEndpoints"/>. Proves the second JWT-bearer scheme validates External ID tokens and
/// surfaces the Student/Preceptor role claims. No DB provisioning here — customer records are owned
/// by the onboarding slices.
/// </summary>
public static class CustomerMeEndpoints
{
    public static IEndpointRouteBuilder MapCustomerMeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/customer/me", (ICurrentUser user) =>
        {
            var roles = user.Roles;

            return Results.Ok(new CustomerMeResponse(
                ObjectId: user.ObjectId ?? string.Empty,
                Name: user.Name,
                Username: user.Username,
                Roles: roles,
                IsStudent: roles.Contains(RoleNames.Student),
                IsPreceptor: roles.Contains(RoleNames.Preceptor)));
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("GetCustomerMe")
        .WithTags("Identity");

        return routes;
    }
}
