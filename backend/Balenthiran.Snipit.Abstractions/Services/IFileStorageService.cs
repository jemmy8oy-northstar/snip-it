namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Local-disk file storage abstraction. Swappable for object storage later — callers only
/// deal in logical storage keys, never raw filesystem paths.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Saves a stream under a new, unique storage key within the given folder. Returns the storage key.</summary>
    Task<string> SaveAsync(Stream content, string folder, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Resolves a storage key to an absolute filesystem path.</summary>
    string GetFullPath(string storageKey);

    Stream OpenRead(string storageKey);
}
