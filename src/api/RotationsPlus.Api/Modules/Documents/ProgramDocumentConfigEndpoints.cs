using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Admin configuration (AdminOnly) of a program's required documents + due-days, and the document-type
/// catalog (list + add custom). Backs the required-docs section of the Program admin form (PHASE 2g-3b).
/// </summary>
public static class ProgramDocumentConfigEndpoints
{
    private const int MaxDueDays = 365;

    public static IEndpointRouteBuilder MapProgramDocumentConfigEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithTags("Documents");

        // The document-type catalog (the checklist options + Add Custom feeds this).
        group.MapGet("/document-types", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var types = await db.DocumentTypes
                .OrderBy(t => t.Category).ThenBy(t => t.Name)
                .Select(t => new DocumentTypeResponse(t.Id, t.Name, t.Category))
                .ToListAsync(cancellationToken);
            return Results.Ok(types);
        })
        .WithName("GetDocumentTypes");

        // Add a custom document type to the catalog ("Add Custom Document Type").
        group.MapPost("/document-types", async (
            CreateDocumentTypeRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.BadRequest("A document type name is required.");
            }
            if (name.Length > 200)
            {
                return Results.BadRequest("The name must be 200 characters or fewer.");
            }

            // Case-insensitive uniqueness (mirrors the unique index; gives a clean 409 instead of a 500).
            var exists = await db.DocumentTypes.AnyAsync(t => t.Name.ToLower() == name.ToLower(), cancellationToken);
            if (exists)
            {
                return Results.Conflict($"A document type named '{name}' already exists.");
            }

            var type = new DocumentType { Name = name, Category = request.Category };
            db.DocumentTypes.Add(type);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/document-types/{type.Id}",
                new DocumentTypeResponse(type.Id, type.Name, type.Category));
        })
        .WithName("CreateDocumentType");

        // A program's required-docs config (due-days + selected type ids) + the catalog to choose from.
        group.MapGet("/programs/{programId:guid}/required-documents", async (
            Guid programId, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var dueDays = await db.Programs
                .Where(p => p.Id == programId)
                .Select(p => (int?)p.DocumentDueDays)
                .FirstOrDefaultAsync(cancellationToken);
            if (dueDays is null)
            {
                return Results.NotFound();
            }

            var requiredIds = await db.ProgramRequiredDocuments
                .Where(prd => prd.ProgramId == programId)
                .Select(prd => prd.DocumentTypeId)
                .ToListAsync(cancellationToken);

            var catalog = await db.DocumentTypes
                .OrderBy(t => t.Category).ThenBy(t => t.Name)
                .Select(t => new DocumentTypeResponse(t.Id, t.Name, t.Category))
                .ToListAsync(cancellationToken);

            return Results.Ok(new ProgramRequiredDocumentsResponse(dueDays.Value, requiredIds, catalog));
        })
        .WithName("GetProgramRequiredDocuments");

        // Set a program's required document types (full replace) + due-days.
        group.MapPut("/programs/{programId:guid}/required-documents", async (
            Guid programId, SetProgramRequiredDocumentsRequest request, RotationsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == programId, cancellationToken);
            if (program is null)
            {
                return Results.NotFound();
            }

            if (request.DocumentDueDays < 0 || request.DocumentDueDays > MaxDueDays)
            {
                return Results.BadRequest($"Due days must be between 0 and {MaxDueDays}.");
            }

            var requestedIds = (request.RequiredDocumentTypeIds ?? []).Distinct().ToList();
            if (requestedIds.Count > 0)
            {
                var validCount = await db.DocumentTypes.CountAsync(t => requestedIds.Contains(t.Id), cancellationToken);
                if (validCount != requestedIds.Count)
                {
                    return Results.BadRequest("One or more document types do not exist.");
                }
            }

            program.DocumentDueDays = request.DocumentDueDays;

            // Reconcile the program's required-document rows against the requested set.
            var existing = await db.ProgramRequiredDocuments
                .Where(prd => prd.ProgramId == programId)
                .ToListAsync(cancellationToken);

            foreach (var row in existing.Where(r => !requestedIds.Contains(r.DocumentTypeId)))
            {
                db.ProgramRequiredDocuments.Remove(row); // soft-deleted by the interceptor
            }

            var existingIds = existing.Select(r => r.DocumentTypeId).ToHashSet();
            foreach (var typeId in requestedIds.Where(id => !existingIds.Contains(id)))
            {
                db.ProgramRequiredDocuments.Add(new ProgramRequiredDocument
                {
                    ProgramId = programId,
                    DocumentTypeId = typeId,
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            var catalog = await db.DocumentTypes
                .OrderBy(t => t.Category).ThenBy(t => t.Name)
                .Select(t => new DocumentTypeResponse(t.Id, t.Name, t.Category))
                .ToListAsync(cancellationToken);

            return Results.Ok(new ProgramRequiredDocumentsResponse(request.DocumentDueDays, requestedIds, catalog));
        })
        .WithName("SetProgramRequiredDocuments");

        return routes;
    }
}
