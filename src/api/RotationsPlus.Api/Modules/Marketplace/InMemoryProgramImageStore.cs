using System.Collections.Concurrent;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// In-memory stand-in for <see cref="IProgramImageStore"/> used when no storage connection is
/// configured (local dev / integration tests). Fully functional — upload stores the bytes and
/// <see cref="GetReadUrl"/> returns a stable, deterministic URL — so the upload/serve flow can be
/// exercised end-to-end without Azure. Contents are process-local and lost on restart.
/// </summary>
public sealed class InMemoryProgramImageStore : IProgramImageStore
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public async Task<string> UploadAsync(Guid programId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var blobName = $"{programId}/{Guid.NewGuid():N}";
        _blobs[blobName] = buffer.ToArray();
        return blobName;
    }

    public Task DeleteAsync(string? blobName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(blobName))
        {
            _blobs.TryRemove(blobName, out _);
        }
        return Task.CompletedTask;
    }

    public string? GetReadUrl(string? blobName) =>
        string.IsNullOrWhiteSpace(blobName) ? null : $"https://images.local/{blobName}";
}
