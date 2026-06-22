using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Customer-facing document endpoints (CustomerOnly): the signed-in student's per-rotation document
/// checklist (with short-lived read URLs for uploaded files) and the upload action. Ownership is matched
/// through the document's student by CIAM oid, so a student can only see/upload their own documents. The
/// admin review (approve/reject) lands in PHASE 2g-3.
/// </summary>
public static class CustomerDocumentEndpoints
{
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB

    public static IEndpointRouteBuilder MapCustomerDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/customer/rotations/{rotationId:guid}/documents", async (
            Guid rotationId, ICurrentUser user, RotationsDbContext db, IDocumentFileStore store,
            CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.Ok(Array.Empty<RotationDocumentResponse>());
            }

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
                    d.FileBlobName, // blob name carried in the FileUrl slot, swapped for a read URL below
                    d.SubmittedAtUtc,
                    d.RejectionReason))
                .ToListAsync(cancellationToken);

            // Mint a short-lived read URL per uploaded file (cheap, no I/O).
            var withUrls = documents.Select(d => d with { FileUrl = store.GetReadUrl(d.FileUrl) }).ToList();
            return Results.Ok(withUrls);
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("GetCustomerRotationDocuments")
        .WithTags("Documents");

        routes.MapPost("/api/customer/rotations/{rotationId:guid}/documents/{documentId:guid}/file", async (
            Guid rotationId, Guid documentId, HttpRequest request, ICurrentUser user, RotationsDbContext db,
            IDocumentFileStore store, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.NotFound();
            }

            // Cap the body before reading the form so an oversized upload is rejected by the transport.
            var maxBodySize = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxBodySize is { IsReadOnly: false })
            {
                maxBodySize.MaxRequestBodySize = MaxFileBytes + (1 * 1024 * 1024);
            }

            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Expected a multipart/form-data upload.");
            }

            IFormFile? file;
            try
            {
                var form = await request.ReadFormAsync(cancellationToken);
                file = form.Files["file"] ?? form.Files.FirstOrDefault();
            }
            catch (BadHttpRequestException)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            catch (InvalidDataException)
            {
                return Results.BadRequest("Expected a valid multipart/form-data upload with a file.");
            }

            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("A file is required.");
            }
            if (file.Length > MaxFileBytes)
            {
                return Results.BadRequest($"File must be {MaxFileBytes / (1024 * 1024)} MB or smaller.");
            }

            // The document must belong to the caller (its student carries the caller's oid) and to the
            // route's rotation. A non-owned/missing doc is a 404 (indistinguishable).
            var document = await db.RotationDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.RotationId == rotationId
                    && db.Students.Any(s => s.Id == d.StudentId && s.StudentOid == oid), cancellationToken);
            if (document is null)
            {
                return Results.NotFound();
            }

            // An approved document is final — the student can't silently replace it (which would also wipe
            // the reviewer's audit). Rejected/Expired/UploadNeeded/Submitted are all re-uploadable (the
            // fix-and-resubmit flow). Admin re-opening of an approved doc, if ever needed, is an admin action.
            if (document.Status == DocumentStatus.Approved)
            {
                return Results.Conflict("This document has already been approved and can't be replaced.");
            }

            // Don't trust the client content type — sniff the magic number; the detected type is both the
            // gate and what we persist/serve. Students upload a PDF or a photo (JPEG/PNG).
            using var buffer = new MemoryStream();
            await using (var stream = file.OpenReadStream())
            {
                await stream.CopyToAsync(buffer, cancellationToken);
            }

            var detectedContentType = DocumentContentTypeDetector.Detect(buffer);
            if (detectedContentType is null)
            {
                return Results.BadRequest("File must be a PDF, Word document, JPEG, PNG, or BMP.");
            }

            var previousBlob = document.FileBlobName;
            buffer.Position = 0;
            var blobName = await store.UploadAsync(document.Id, buffer, detectedContentType, cancellationToken);

            // Submitting (re)starts the review: clear any prior rejection/review and mark Submitted.
            document.FileBlobName = blobName;
            document.FileName = SafeFileName(file.FileName);
            document.Status = DocumentStatus.Submitted;
            document.SubmittedAtUtc = clock.GetUtcNow();
            document.RejectionReason = null;
            document.ReviewedAtUtc = null;
            document.ReviewedBy = null;
            await db.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(previousBlob) && previousBlob != blobName)
            {
                await store.DeleteAsync(previousBlob, cancellationToken);
            }

            var response = new RotationDocumentResponse(
                document.Id, await DocumentTypeNameAsync(db, document.DocumentTypeId, cancellationToken),
                await DocumentCategoryAsync(db, document.DocumentTypeId, cancellationToken),
                document.Status, document.DueDate, document.FileName, store.GetReadUrl(blobName),
                document.SubmittedAtUtc, document.RejectionReason);
            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .DisableAntiforgery() // token-authenticated API (no cookies); CustomerOnly + ownership is the gate.
        .Accepts<IFormFile>("multipart/form-data")
        .WithName("UploadRotationDocument")
        .WithTags("Documents");

        return routes;
    }

    private static Task<string> DocumentTypeNameAsync(RotationsDbContext db, Guid typeId, CancellationToken ct) =>
        db.DocumentTypes.Where(t => t.Id == typeId).Select(t => t.Name).FirstAsync(ct);

    private static Task<DocumentCategory> DocumentCategoryAsync(RotationsDbContext db, Guid typeId, CancellationToken ct) =>
        db.DocumentTypes.Where(t => t.Id == typeId).Select(t => t.Category).FirstAsync(ct);

    /// <summary>Strips any path and caps length so a malicious filename can't traverse or bloat the row.
    /// The name is display-only (the served content type comes from the sniffed magic bytes).</summary>
    private static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "document";
        }
        return name.Length > 200 ? name[..200] : name;
    }
}
