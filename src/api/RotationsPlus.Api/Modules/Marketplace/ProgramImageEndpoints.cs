using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Admin-only management of a program's hospital image: upload (set/replace) and delete. The image
/// bytes live in blob storage (<see cref="IProgramImageStore"/>); the program row keeps only the blob
/// name. Reads happen through the catalog endpoints, which mint a short-lived read URL.
/// </summary>
public static class ProgramImageEndpoints
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    public static IEndpointRouteBuilder MapProgramImageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/programs/{id:guid}/image")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithTags("Marketplace");

        group.MapPost("/", async (
            Guid id, HttpRequest request, RotationsDbContext db, IProgramImageStore store, CancellationToken cancellationToken) =>
        {
            // Cap the request body BEFORE the form is read so an oversized upload is rejected by the
            // transport instead of being buffered to disk first. Set just above the 5 MB content limit to
            // leave room for multipart framing. (Under the in-memory TestServer the feature is read-only/
            // absent and this is a no-op; Kestrel enforces it in the deployed app.)
            var maxBodySize = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxBodySize is { IsReadOnly: false })
            {
                maxBodySize.MaxRequestBodySize = MaxImageBytes + (1 * 1024 * 1024);
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
                // Body exceeded the cap — reject without buffering it all.
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            catch (InvalidDataException)
            {
                // Malformed multipart payload (e.g. missing/invalid Content-Disposition, or an empty
                // form with no file part) — a client error, not a server fault.
                return Results.BadRequest("Expected a valid multipart/form-data upload with an image file.");
            }

            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("An image file is required.");
            }
            if (file.Length > MaxImageBytes)
            {
                return Results.BadRequest($"Image must be {MaxImageBytes / (1024 * 1024)} MB or smaller.");
            }

            // Don't trust the client-declared content type. Buffer the (already size-capped) bytes and
            // sniff the magic number; the detected type is both the gate AND what we persist/serve, so a
            // polyglot or mislabelled file can't be stored as an image and later MIME-sniffed as active
            // content. Unknown signature → rejected.
            using var buffer = new MemoryStream();
            await using (var stream = file.OpenReadStream())
            {
                await stream.CopyToAsync(buffer, cancellationToken);
            }

            var detectedContentType = DetectImageContentType(buffer);
            if (detectedContentType is null)
            {
                return Results.BadRequest("Image must be a JPEG, PNG, or WebP.");
            }

            var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (program is null)
            {
                return Results.NotFound();
            }

            var previousBlob = program.ImageBlobName;

            buffer.Position = 0;
            var blobName = await store.UploadAsync(id, buffer, detectedContentType, cancellationToken);

            // Persist the new pointer first; only then drop the old blob, so a save failure leaves the
            // (still-referenced) previous image intact rather than orphaning the row's pointer.
            program.ImageBlobName = blobName;
            await db.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(previousBlob) && previousBlob != blobName)
            {
                await store.DeleteAsync(previousBlob, cancellationToken);
            }

            return Results.Ok(new ProgramImageResponse(store.GetReadUrl(blobName)));
        })
        .DisableAntiforgery() // token-authenticated API (no cookies); the AdminOnly policy is the gate.
        .Accepts<IFormFile>("multipart/form-data")
        .WithName("UploadProgramImage");

        group.MapDelete("/", async (
            Guid id, RotationsDbContext db, IProgramImageStore store, CancellationToken cancellationToken) =>
        {
            var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (program is null)
            {
                return Results.NotFound();
            }

            var blob = program.ImageBlobName;
            if (string.IsNullOrWhiteSpace(blob))
            {
                return Results.NoContent(); // idempotent: nothing to remove
            }

            program.ImageBlobName = null;
            await db.SaveChangesAsync(cancellationToken);
            await store.DeleteAsync(blob, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteProgramImage");

        return routes;
    }

    /// <summary>Detects the image type from the leading bytes (magic number), returning the canonical
    /// content type for JPEG/PNG/WebP or null if the bytes don't match a supported image. This is the
    /// authoritative check — the client-declared content type is never trusted for gating or serving.</summary>
    private static string? DetectImageContentType(Stream stream)
    {
        stream.Position = 0;
        Span<byte> header = stackalloc byte[12];
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        if (read < 12)
        {
            return null; // too short to be any supported image
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "image/png";
        }

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // WebP: "RIFF" .... "WEBP"
        if (header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
        {
            return "image/webp";
        }

        return null;
    }
}
