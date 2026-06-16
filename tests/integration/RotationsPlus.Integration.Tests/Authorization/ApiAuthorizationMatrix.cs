using RotationsPlus.Common.Authorization;

namespace RotationsPlus.Integration.Tests.Authorization;

/// <summary>One row of the authorization matrix: an endpoint and the roles allowed to reach it.</summary>
public sealed record EndpointSpec(string Method, string Path, string[] AllowedRoles, string Description);

/// <summary>
/// The single source of truth for "every endpoint × role" (CLAUDE.md §3 merge gate).
/// Add a row here whenever a new endpoint lands — <see cref="AuthorizationMatrixTests"/> then
/// enforces, for every known role, that allowed roles get through and everyone else is rejected.
/// </summary>
public static class ApiAuthorizationMatrix
{
    public static readonly EndpointSpec[] Endpoints =
    [
        new("GET", "/api/me", RoleNames.Staff, "Current staff identity + provisioned profile"),
        new("GET", "/api/specialties", RoleNames.Staff, "List marketplace specialties"),
        // A seeded id, so an authorized caller routes through to a real resource (not a 404).
        new("GET", "/api/specialties/aaaaaaaa-0000-0000-0000-000000000001", RoleNames.Staff, "Get specialty by id"),
        // Admin-only writes. Non-existent id / empty body → authorized callers get 404/400 (not 401/403),
        // which the authz-only matrix accepts; endpoint behaviour is covered by SpecialtyAdminEndpointTests.
        new("POST", "/api/specialties", [RoleNames.Admin], "Create specialty (admin)"),
        new("PUT", "/api/specialties/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update specialty (admin)"),
        new("DELETE", "/api/specialties/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete specialty (admin)"),
        new("GET", "/api/programs", RoleNames.Staff, "List marketplace programs"),
        new("GET", "/api/programs/cccccccc-0000-0000-0000-000000000001", RoleNames.Staff, "Get program by id"),
        // Admin-only writes. Non-existent id / empty body → authorized callers get 404/400 (not 401/403),
        // which the authz-only matrix accepts; endpoint behaviour is covered by ProgramAdminEndpointTests.
        new("POST", "/api/programs", [RoleNames.Admin], "Create program (admin)"),
        new("PUT", "/api/programs/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update program (admin)"),
        new("DELETE", "/api/programs/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete program (admin)"),
        new("GET", "/api/preceptors", RoleNames.Staff, "List marketplace preceptors"),
        new("GET", "/api/preceptors/dddddddd-0000-0000-0000-000000000001", RoleNames.Staff, "Get preceptor by id"),
        new("POST", "/api/preceptors", [RoleNames.Admin], "Create preceptor (admin)"),
        new("PUT", "/api/preceptors/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update preceptor (admin)"),
        new("DELETE", "/api/preceptors/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete preceptor (admin)"),
    ];

    /// <summary>Every role the system issues, across both Entra directories.</summary>
    public static readonly string[] AllRoles = [.. RoleNames.Staff, .. RoleNames.Customer];
}
