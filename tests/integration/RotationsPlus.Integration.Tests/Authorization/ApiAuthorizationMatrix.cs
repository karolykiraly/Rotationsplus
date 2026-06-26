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
        // Self-booking is CustomerOnly. An empty body / no linked student profile → authorized customers
        // get 400 (not 401/403), which the authz-only matrix accepts; behaviour is covered by
        // CustomerBookingEndpointTests.
        new("POST", "/api/customer/rotations", RoleNames.Customer, "Student self-books a rotation"),
        // The signed-in student's per-rotation document checklist. A non-owned/seeded rotation yields an
        // empty 200 for an authorized customer (authorized-through), which the authz-only matrix accepts.
        new("GET", "/api/customer/rotations/eeeeeeee-0000-0000-0000-000000000001/documents", RoleNames.Customer, "The student's rotation documents"),
        // Upload a file for a rotation document. An authorized non-owner customer gets 404 (empty body →
        // 400 once owned); both are authorized-through, which the authz-only matrix accepts.
        new("POST", "/api/customer/rotations/eeeeeeee-0000-0000-0000-000000000001/documents/d40c0000-0000-0000-0000-000000000003/file", RoleNames.Customer, "Student uploads a rotation document"),
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
        // Server-computed price quote — open to marketplace viewers (students need pricing to book).
        new("GET", "/api/programs/cccccccc-0000-0000-0000-000000000001/quote?weeks=4", MarketplaceReaders, "Program price quote"),
        // Admin-only writes. Non-existent id / empty body → authorized callers get 404/400 (not 401/403),
        // which the authz-only matrix accepts; endpoint behaviour is covered by ProgramAdminEndpointTests.
        new("POST", "/api/programs", [RoleNames.Admin], "Create program (admin)"),
        new("PUT", "/api/programs/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update program (admin)"),
        new("DELETE", "/api/programs/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete program (admin)"),
        // Program image upload/delete are AdminOnly. Empty body / non-existent id → authorized admins get
        // 400/404 (not 401/403), which the authz-only matrix accepts; behaviour is in ProgramImageEndpointTests.
        new("POST", "/api/programs/00000000-0000-0000-0000-000000000000/image", [RoleNames.Admin], "Upload program image (admin)"),
        new("DELETE", "/api/programs/00000000-0000-0000-0000-000000000000/image", [RoleNames.Admin], "Delete program image (admin)"),
        new("GET", "/api/preceptors", RoleNames.Staff, "List marketplace preceptors"),
        new("GET", "/api/preceptors/options", RoleNames.Staff, "List preceptor options for form pickers (staff)"),
        new("GET", "/api/preceptors/dddddddd-0000-0000-0000-000000000001", RoleNames.Staff, "Get preceptor by id"),
        new("POST", "/api/preceptors", [RoleNames.Admin], "Create preceptor (admin)"),
        new("PUT", "/api/preceptors/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update preceptor (admin)"),
        new("DELETE", "/api/preceptors/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete preceptor (admin)"),
        // Rotation management is AdminOnly (reads too — students see their own via the portal later).
        new("GET", "/api/rotations", [RoleNames.Admin], "List rotations (admin)"),
        new("GET", "/api/rotations/eeeeeeee-0000-0000-0000-000000000001", [RoleNames.Admin], "Get rotation by id (admin)"),
        // Opening a deposit is CustomerOnly (the student pays for their own rotation). A customer who
        // doesn't own this seeded rotation gets 404 — authorized-through, which the matrix accepts;
        // ownership + fulfilment are covered by PaymentCheckoutEndpointTests. The Stripe webhook is
        // intentionally anonymous (signature-verified) so it is NOT a matrix endpoint.
        new("POST", "/api/rotations/eeeeeeee-0000-0000-0000-000000000001/payment-intent", RoleNames.Customer, "Open rotation deposit (customer)"),
        // DEV/test-only simulate endpoint (mapped on non-Production; see Program.cs). CustomerOnly; an
        // empty body / non-owned payment → authorized callers get 400/404 (not 401/403), which the
        // authz-only matrix accepts. Behaviour is covered by PaymentCheckoutEndpointTests.
        new("POST", "/api/dev/payments/eeeeeeee-0000-0000-0000-000000000001/simulate", RoleNames.Customer, "DEV: simulate deposit outcome (customer)"),
        // Refunding is AdminOnly. The seeded rotation isn't in a refundable state, so an authorized admin
        // gets 409 (not 401/403), which the authz-only matrix accepts; behaviour is in PaymentRefundEndpointTests.
        new("POST", "/api/rotations/eeeeeeee-0000-0000-0000-000000000001/refund", [RoleNames.Admin], "Refund a rotation (admin)"),
        new("POST", "/api/rotations", [RoleNames.Admin], "Create rotation (admin)"),
        new("PUT", "/api/rotations/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update rotation (admin)"),
        new("DELETE", "/api/rotations/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete rotation (admin)"),
        // Admin hub aggregate (totals + rotation pipeline + upcoming starts).
        new("GET", "/api/dashboard", [RoleNames.Admin], "Admin dashboard aggregate"),
        new("GET", "/api/dashboard/todos", [RoleNames.Admin], "Admin dashboard to-do queues"),
        new("GET", "/api/dashboard/revenue", [RoleNames.Admin], "Admin dashboard revenue"),
        new("GET", "/api/dashboard/reports", [RoleNames.Admin], "Admin dashboard reports"),
        // Email campaigns (compose/list/send) — AdminOnly.
        new("GET", "/api/campaigns", [RoleNames.Admin], "List campaigns (admin)"),
        new("POST", "/api/campaigns/00000000-0000-0000-0000-000000000000/send", [RoleNames.Admin], "Send campaign (admin)"),
        // Student directory: reads StaffOnly (sales/SDR/coordinator work the directory for CRM), writes AdminOnly.
        new("GET", "/api/students", RoleNames.Staff, "List students (staff)"),
        new("GET", "/api/students/options", RoleNames.Staff, "List student options for form pickers (staff)"),
        new("GET", "/api/students/ffffffff-0000-0000-0000-000000000001", RoleNames.Staff, "Get student by id (staff)"),
        new("POST", "/api/students", [RoleNames.Admin], "Create student (admin)"),
        new("PUT", "/api/students/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Update student (admin)"),
        new("DELETE", "/api/students/00000000-0000-0000-0000-000000000000", [RoleNames.Admin], "Delete student (admin)"),
        // Admin document review + config (PHASE 2g-3a) — all AdminOnly. Seeded ids route through to real
        // resources; non-existent ids / empty bodies give authorized admins 404/400 (authorized-through).
        new("GET", "/api/students/ffffffff-0000-0000-0000-000000000001/documents", [RoleNames.Admin], "A student's documents (admin review)"),
        new("PUT", "/api/documents/d40c0000-0000-0000-0000-000000000003/status", [RoleNames.Admin], "Set a document's status (admin)"),
        new("POST", "/api/documents/d40c0000-0000-0000-0000-000000000003/file", [RoleNames.Admin], "Admin uploads a document on behalf"),
        new("DELETE", "/api/documents/00000000-0000-0000-0000-000000000000/file", [RoleNames.Admin], "Admin clears a document file"),
        new("GET", "/api/document-types", [RoleNames.Admin], "Document-type catalog (admin)"),
        new("POST", "/api/document-types", [RoleNames.Admin], "Add a custom document type (admin)"),
        new("GET", "/api/programs/cccccccc-0000-0000-0000-000000000001/required-documents", [RoleNames.Admin], "A program's required-docs config (admin)"),
        new("PUT", "/api/programs/cccccccc-0000-0000-0000-000000000001/required-documents", [RoleNames.Admin], "Set a program's required-docs config (admin)"),
    ];

    /// <summary>Every role the system issues, across both Entra directories.</summary>
    public static readonly string[] AllRoles = [.. RoleNames.Staff, .. RoleNames.Customer];
}
