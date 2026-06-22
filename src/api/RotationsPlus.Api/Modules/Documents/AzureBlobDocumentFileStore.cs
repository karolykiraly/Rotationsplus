using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Azure Blob-backed document store. Same private-account + account-key service-SAS approach as the
/// program-image store (PHASE 2b) — the account stays private; reads go through short-lived SAS URLs
/// signed with the account key (delivered via Key Vault because the deploy principal is Contributor-only
/// and can't grant the role assignment a user-delegation SAS would need).
/// </summary>
public sealed class AzureBlobDocumentFileStore : IDocumentFileStore
{
    private readonly BlobContainerClient _container;
    private readonly StorageSharedKeyCredential _sharedKey;
    private readonly string _containerName;
    private readonly int _sasTtlMinutes;
    private readonly TimeProvider _clock;

    public AzureBlobDocumentFileStore(IOptions<DocumentFileOptions> options, TimeProvider clock)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString))
        {
            throw new InvalidOperationException("Storage:Documents:ConnectionString is required for the Azure document store.");
        }

        var (accountName, accountKey) = ParseAccountNameAndKey(o.ConnectionString);
        _sharedKey = new StorageSharedKeyCredential(accountName, accountKey);

        _container = new BlobContainerClient(o.ConnectionString, o.ContainerName);
        _containerName = o.ContainerName;
        _sasTtlMinutes = o.SasTtlMinutes;
        _clock = clock;
    }

    public async Task<string> UploadAsync(Guid rotationDocumentId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // Foldered by document id; a fresh GUID per upload means a re-upload gets a new URL (no stale cache).
        var blobName = $"{rotationDocumentId}/{Guid.NewGuid():N}{DocumentContentTypeDetector.ExtensionFor(contentType)}";
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
