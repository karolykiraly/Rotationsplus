using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Customer-facing document endpoints (CustomerOnly): the signed-in student's per-rotation document
/// checklist. The caller is matched to the rotation through their directory <c>Student</c> by CIAM oid,
/// so a student can only see their own documents (a non-owned/missing rotation is an indistinguishable
/// empty result). The upload + file-serve path lands in PHASE 2g-2.
/// </summary>
public static class CustomerDocumentEndpoints
{
    public static IEndpointRouteBuilder MapCustomerDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/customer/rotations/{rotationId:guid}/documents", async (
            Guid rotationId, ICurrentUser user, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.Ok(Array.Empty<RotationDocumentResponse>());
            }

            // Ownership: the rotation must belong to the caller's directory student (matched by oid).
            var owns = await db.Rotations.AnyAsync(
                r => r.Id == rotationId && db.Students.Any(s => s.Id == r.StudentId && s.StudentOid == oid),
                cancellationToken);
            if (!owns)
            {
                return Results.Ok(Array.Empty<RotationDocumentResponse>());
            }

            var documents = await db.RotationDocuments
                .Where(d => d.RotationId == rotationId)
                .OrderBy(d => d.DocumentType.Category)
                .ThenBy(d => d.DocumentType.Name)
                .Select(d => new RotationDocumentResponse(
                    d.Id,
                    d.DocumentType.Name,
                    d.DocumentType.Category,
                    d.Status,
                    d.DueDate,
                    d.FileName,
                    d.SubmittedAtUtc,
                    d.RejectionReason))
                .ToListAsync(cancellationToken);

            return Results.Ok(documents);
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("GetCustomerRotationDocuments")
        .WithTags("Documents");

        return routes;
    }
}
