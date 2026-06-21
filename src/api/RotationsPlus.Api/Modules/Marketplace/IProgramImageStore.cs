namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Stores program/hospital images and mints short-lived read URLs for them. Backed by Azure Blob
/// storage on DEV/PROD (<see cref="AzureBlobProgramImageStore"/>); an in-memory stand-in
/// (<see cref="InMemoryProgramImageStore"/>) is used when no storage connection is configured
/// (local dev / integration tests) so upload + serve still work end-to-end.
/// </summary>
public interface IProgramImageStore
{
    /// <summary>Uploads image bytes for a program and returns the opaque blob name to persist.</summary>
    Task<string> UploadAsync(Guid programId, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Removes a stored image. No-op if the blob name is null/blank or already gone.</summary>
    Task DeleteAsync(string? blobName, CancellationToken cancellationToken);

    /// <summary>A time-limited read URL for the blob, or null when <paramref name="blobName"/> is
    /// null/blank. Cheap (no I/O) — safe to call per row when projecting a catalog list.</summary>
    string? GetReadUrl(string? blobName);
}

/// <summary>Binds <c>Storage:Images</c>. <c>ConnectionString</c> is injected from Key Vault on DEV/PROD
/// (see infra/bicep/main.bicep); when absent the in-memory store is registered instead.</summary>
public sealed class ProgramImageOptions
{
    public const string SectionName = "Storage:Images";

    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "program-images";

    /// <summary>Lifetime of a minted read SAS URL. Long enough to render a page; short enough that a
    /// leaked URL expires quickly.</summary>
    public int SasTtlMinutes { get; set; } = 60;
}
