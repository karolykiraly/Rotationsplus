using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Azure Blob-backed image store. The account stays private (no anonymous access); reads are served
/// through short-lived service SAS URLs signed with the account key. Account-key SAS (rather than a
/// managed-identity user-delegation SAS) is a deliberate choice — the deploy pipeline principal is only
/// Contributor and cannot create the role assignment user-delegation would require, so the account-key
/// connection string is delivered via Key Vault instead (see infra/bicep/main.bicep).
/// </summary>
public sealed class AzureBlobProgramImageStore : IProgramImageStore
{
    private readonly BlobContainerClient _container;
    private readonly StorageSharedKeyCredential _sharedKey;
    private readonly string _containerName;
    private readonly int _sasTtlMinutes;
    private readonly TimeProvider _clock;

    public AzureBlobProgramImageStore(IOptions<ProgramImageOptions> options, TimeProvider clock)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString))
        {
            throw new InvalidOperationException("Storage:Images:ConnectionString is required for the Azure image store.");
        }

        // Validate/parse the connection string up front so a malformed one (e.g. missing AccountKey)
        // surfaces as a clear InvalidOperationException rather than an SDK-specific failure later.
        var (accountName, accountKey) = ParseAccountNameAndKey(o.ConnectionString);
        _sharedKey = new StorageSharedKeyCredential(accountName, accountKey);

        _container = new BlobContainerClient(o.ConnectionString, o.ContainerName);
        _containerName = o.ContainerName;
        _sasTtlMinutes = o.SasTtlMinutes;
        _clock = clock;
    }

    public async Task<string> UploadAsync(Guid programId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // Foldered by program id; a fresh GUID per upload means a replaced image gets a new URL (no stale CDN/cache).
        var blobName = $"{programId}/{Guid.NewGuid():N}{ExtensionFor(contentType)}";
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
        return blobName;
    }

    public Task DeleteAsync(string? blobName, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(blobName)
            ? Task.CompletedTask
            : _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    public string? GetReadUrl(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        var sas = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = now.AddMinutes(-5),            // small back-date to tolerate clock skew
            ExpiresOn = now.AddMinutes(_sasTtlMinutes),
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        var blob = _container.GetBlobClient(blobName);
        var token = sas.ToSasQueryParameters(_sharedKey).ToString();
        return $"{blob.Uri}?{token}";
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => string.Empty,
    };

    private static (string AccountName, string AccountKey) ParseAccountNameAndKey(string connectionString)
    {
        string? name = null, key = null;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var k = part[..eq].Trim();
            var v = part[(eq + 1)..].Trim();
            if (k.Equals("AccountName", StringComparison.OrdinalIgnoreCase)) name = v;
            else if (k.Equals("AccountKey", StringComparison.OrdinalIgnoreCase)) key = v;
        }

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Storage connection string must contain AccountName and AccountKey for SAS signing.");
        }

        return (name, key);
    }
}
