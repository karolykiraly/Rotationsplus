using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Rotations;

/// <summary>
/// GET /api/customer/rotations — the signed-in student's own rotations ("My rotations" in the portal).
/// CustomerOnly. The caller is matched to their directory <c>Student</c> by CIAM oid (so the link works
/// even if the admin set the oid after booking); rotations in the student-hidden lifecycle states
/// (Cancelled / Refunded / Abandoned / Rejected, per Plan_Student §7) are excluded from the tracker.
/// </summary>
public static class CustomerRotationEndpoints
{
    public static IEndpointRouteBuilder MapCustomerRotationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/customer/rotations", async (ICurrentUser user, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.Ok(Array.Empty<CustomerRotationResponse>());
            }

            var rotations = await db.Rotations
                // Match through the live directory record by oid (not the rotation's snapshot oid).
                .Where(r => db.Students.Any(s => s.Id == r.StudentId && s.StudentOid == oid))
                // Hide the terminal/negative states from the student tracker (Plan_Student §7).
                .Where(r => r.Status != RotationStatus.Cancelled
                         && r.Status != RotationStatus.Refunded
                         && r.Status != RotationStatus.Abandoned
                         && r.Status != RotationStatus.Rejected)
                .OrderByDescending(r => r.StartDate)
                .Select(r => new CustomerRotationResponse(
                    r.Id,
                    r.Program.Specialty.Name,
                    r.Program.ProgramType,
                    r.Program.Preceptor != null ? r.Program.Preceptor.FirstName + " " + r.Program.Preceptor.LastName : null,
                    r.StartDate,
                    r.EndDate,
                    r.Weeks,
                    r.Status))
                .ToListAsync(cancellationToken);

            return Results.Ok(rotations);
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("GetCustomerRotations")
        .WithTags("Rotations");

        return routes;
    }
}
