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

    /// <summary>
    /// Opens the stored file for reading, or returns <c>null</c> if there is nothing at that key.
    /// </summary>
    /// <remarks>
    /// Missing is a normal answer, not a failure: storage keys live in database rows that outlive
    /// the bytes they point at, so callers must be able to say "gone" without an exception. This
    /// deliberately has no companion <c>Exists</c> — checking first and opening second is a race
    /// anything deleting in the background (a retention sweep) would lose, so the open itself is
    /// the check.
    /// </remarks>
    Stream? TryOpenRead(string storageKey);
}
