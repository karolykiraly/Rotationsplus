using System.Security.Claims;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;

namespace RotationsPlus.Api.Infrastructure;

/// <summary>ICurrentUser over the ambient HttpContext principal.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? ObjectId =>
        Principal?.FindFirstValue(ClaimNames.ObjectId)
        ?? Principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

    public string? Name => Principal?.FindFirstValue(ClaimNames.Name);

    public string? Username => Principal?.FindFirstValue(ClaimNames.PreferredUsername);

    public IReadOnlyList<string> Roles
    {
        get
        {
            var p = Principal;
            if (p is null)
            {
                return [];
            }

            // Cover both the mapped ClaimTypes.Role and the raw "roles" claim, depending on RoleClaimType config.
            return p.FindAll(ClaimTypes.Role)
                .Concat(p.FindAll(ClaimNames.Roles))
                .Select(c => c.Value)
                .Distinct()
                .ToArray();
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Roles.Contains(role);
}
