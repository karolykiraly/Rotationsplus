using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Admin document review (AdminOnly) — the Student-Profile "Documents" screen. Lists a student's
/// documents (with rotation context), sets a document's status (the review dropdown, with a rejection
/// reason), and lets an admin upload/replace or clear a file on the student's behalf. The student-facing
/// upload lives in <see cref="CustomerDocumentEndpoints"/>.
/// </summary>
public static class AdminDocumentEndpoints
{
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB

    public static IEndpointRouteBuilder MapAdminDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithTags("Documents");

        // A student's documents across their rotations (the admin review tab filters by rotation number).
        group.MapGet("/students/{studentId:guid}/documents", async (
            Guid studentId, RotationsDbContext db, IDocumentFileStore store, CancellationToken cancellationToken) =>
        {
            var documents = await db.RotationDocuments
                .Where(d => d.StudentId == studentId)
                .OrderByDescending(d => d.Rotation.RotationNumber)
                .ThenBy(d => d.DocumentType.Category)
                .ThenBy(d => d.DocumentType.Name)
                .Select(d => new AdminRotationDocumentResponse(
                    d.Id, d.RotationId, d.Rotation.RotationNumber, d.DocumentType.Name, d.DocumentType.Category,
                    d.Status, d.DueDate, d.FileName, d.FileBlobName, d.SubmittedAtUtc, d.ReviewedAtUtc, d.RejectionReason))
                .ToListAsync(cancellationToken);

            var withUrls = documents.Select(d => d with { FileUrl = store.GetReadUrl(d.FileUrl) }).ToList();
            return Results.Ok(withUrls);
        })
        .WithName("GetStudentDocuments");

        // Set a document's status (the review dropdown). Rejection keeps the uploaded file so the student
        // can see what was rejected; the reason is shown to them. Stamps the reviewer + time.
        group.MapPut("/documents/{documentId:guid}/status", async (
            Guid documentId, SetDocumentStatusRequest request, RotationsDbContext db, ICurrentUser user,
            TimeProvider clock, IDocumentFileStore store, CancellationToken cancellationToken) =>
        {
            // Reject an out-of-range enum (the JSON converter accepts raw numbers, which would otherwise
            // persist as a bogus string via the value converter).
            if (!Enum.IsDefined(request.Status))
            {
                return Results.BadRequest("Unknown document status.");
            }

            var document = await db.RotationDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document is null)
            {
                return Results.NotFound();
            }

            document.Status = request.Status;
            document.RejectionReason = request.Status == DocumentStatus.Rejected
                ? request.RejectionReason?.Trim()
                : null;
            document.ReviewedAtUtc = clock.GetUtcNow();
            document.ReviewedBy = user.ObjectId;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ToResponseAsync(db, store, document.Id, cancellationToken));
        })
        .WithName("SetDocumentStatus");

        // Admin uploads a file on the student's behalf (same magic-byte validation as the student upload).
        group.MapPost("/documents/{documentId:guid}/file", async (
            Guid documentId, HttpRequest request, RotationsDbContext db, TimeProvider clock,
            IDocumentFileStore store, CancellationToken cancellationToken) =>
        {
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

            var document = await db.RotationDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document is null)
            {
                return Results.NotFound();
            }

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

            document.FileBlobName = blobName;
            document.FileName = SafeFileName(file.FileName);
            document.Status = DocumentStatus.Submitted;
            document.SubmittedAtUtc = clock.GetUtcNow();
            document.RejectionReason = null;
            await db.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(previousBlob) && previousBlob != blobName)
            {
                await store.DeleteAsync(previousBlob, cancellationToken);
            }

            return Results.Ok(await ToResponseAsync(db, store, document.Id, cancellationToken));
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .WithName("AdminUploadDocument");

        // Clear a document's file (admin) — resets it to UploadNeeded.
        group.MapDelete("/documents/{documentId:guid}/file", async (
            Guid documentId, RotationsDbContext db, IDocumentFileStore store, CancellationToken cancellationToken) =>
        {
            var document = await db.RotationDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document is null)
            {
                return Results.NotFound();
            }

            var blob = document.FileBlobName;
            document.FileBlobName = null;
            document.FileName = null;
            document.Status = DocumentStatus.UploadNeeded;
            document.SubmittedAtUtc = null;
            document.ReviewedAtUtc = null;
            document.ReviewedBy = null;
            document.RejectionReason = null;
            await db.SaveChangesAsync(cancellationToken);

            await store.DeleteAsync(blob, cancellationToken);

            return Results.Ok(await ToResponseAsync(db, store, document.Id, cancellationToken));
        })
        .WithName("AdminDeleteDocumentFile");

        return routes;
    }

    private static async Task<AdminRotationDocumentResponse> ToResponseAsync(
        RotationsDbContext db, IDocumentFileStore store, Guid documentId, CancellationToken cancellationToken)
    {
        var d = await db.RotationDocuments
            .Where(x => x.Id == documentId)
            .Select(x => new AdminRotationDocumentResponse(
                x.Id, x.RotationId, x.Rotation.RotationNumber, x.DocumentType.Name, x.DocumentType.Category,
                x.Status, x.DueDate, x.FileName, x.FileBlobName, x.SubmittedAtUtc, x.ReviewedAtUtc, x.RejectionReason))
            .FirstAsync(cancellationToken);
        return d with { FileUrl = store.GetReadUrl(d.FileUrl) };
    }

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
