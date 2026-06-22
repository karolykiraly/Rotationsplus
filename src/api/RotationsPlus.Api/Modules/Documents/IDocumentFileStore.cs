namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Stores student-uploaded document files and mints short-lived read URLs for them. Backed by Azure
/// Blob storage on DEV/PROD (<see cref="AzureBlobDocumentFileStore"/>); an in-memory stand-in
/// (<see cref="InMemoryDocumentFileStore"/>) is used when no storage connection is configured
/// (local dev / integration tests) so upload + serve still work end-to-end. Mirrors the program-image
/// store pattern (PHASE 2b), on the same private account but a separate <c>documents</c> container.
/// </summary>
public interface IDocumentFileStore
{
    /// <summary>Uploads file bytes for a rotation document and returns the opaque blob name to persist.</summary>
    Task<string> UploadAsync(Guid rotationDocumentId, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Removes a stored file. No-op if the blob name is null/blank or already gone.</summary>
    Task DeleteAsync(string? blobName, CancellationToken cancellationToken);

    /// <summary>A time-limited read URL for the blob, or null when <paramref name="blobName"/> is
    /// null/blank. Cheap (no I/O) — safe to call per row when projecting a checklist.</summary>
    string? GetReadUrl(string? blobName);
}

/// <summary>Binds <c>Storage:Documents</c>. <c>ConnectionString</c> is injected from Key Vault on
/// DEV/PROD (the same <c>blob-connection</c> secret as images; see infra/bicep/main.bicep); when absent
/// the in-memory store is registered instead.</summary>
public sealed class DocumentFileOptions
{
    public const string SectionName = "Storage:Documents";

    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "documents";

    /// <summary>Lifetime of a minted read SAS URL.</summary>
    public int SasTtlMinutes { get; set; } = 60;
}
