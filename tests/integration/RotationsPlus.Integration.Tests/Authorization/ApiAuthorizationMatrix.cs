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
    /// <summary>Roles allowed to read the marketplace catalog (specialties/programs): staff + customers.</summary>
    public static readonly string[] MarketplaceReaders = [.. RoleNames.Staff, .. RoleNames.Customer];

    public static readonly EndpointSpec[] Endpoints =
    [
        new("GET", "/api/me", RoleNames.Staff, "Current staff identity + provisioned profile"),
        new("GET", "/api/customer/me", RoleNames.Customer, "Current customer (Student/Preceptor) identity"),
        // Returns the caller's own rotations (empty for a customer with none) → 200 for customers, 403 for staff.
        new("GET", "/api/customer/rotations", RoleNames.Customer, "The signed-in customer's rotations"),
        // Catalog reads are open to any marketplace viewer (staff + customers).
        new("GET", "/api/specialties", MarketplaceReaders, "List marketplace specialties"),
        // A seeded id, so an authorized caller routes through to a real resource (not a 404).
        new("GET", "/api/specialties/aaaaaaaa-0000-0000-0000-000000000001", MarketplaceReaders, "Get specialty by id"),
        // Admin-only writes. Non-existent id / empty body → authorized callers get 404/400 (not 401/403),
        // which the authz-only matrix accepts; endpoint behaviour is covered by SpecialtyAdminEndpointTests.
        new("POST", "/api/specialties", [RoleNames.Admin], "Create specialty (admin)"),
        new("PUT", "/api/specialties/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update specialty (admin)"),
        new("DELETE", "/api/specialties/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete specialty (admin)"),
        new("GET", "/api/programs", MarketplaceReaders, "List marketplace programs"),
        new("GET", "/api/programs/cccccccc-0000-0000-0000-000000000001", MarketplaceReaders, "Get program by id"),
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
        // Rotation management is AdminOnly (reads too — students see their own via the portal later).
        new("GET", "/api/rotations", [RoleNames.Admin], "List rotations (admin)"),
        new("GET", "/api/rotations/eeeeeeee-0000-0000-0000-000000000001", [RoleNames.Admin], "Get rotation by id (admin)"),
        new("POST", "/api/rotations", [RoleNames.Admin], "Create rotation (admin)"),
        new("PUT", "/api/rotations/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update rotation (admin)"),
        new("DELETE", "/api/rotations/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete rotation (admin)"),
        // Student directory: reads StaffOnly (sales/SDR/coordinator work the directory for CRM), writes AdminOnly.
        new("GET", "/api/students", RoleNames.Staff, "List students (staff)"),
        new("GET", "/api/students/ffffffff-0000-0000-0000-000000000001", RoleNames.Staff, "Get student by id (staff)"),
        new("POST", "/api/students", [RoleNames.Admin], "Create student (admin)"),
        new("PUT", "/api/students/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update student (admin)"),
        new("DELETE", "/api/students/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete student (admin)"),
    ];

    /// <summary>Every role the system issues, across both Entra directories.</summary>
    public static readonly string[] AllRoles = [.. RoleNames.Staff, .. RoleNames.Customer];
}
