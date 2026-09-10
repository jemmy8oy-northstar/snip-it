namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Scratch file storage abstraction. Swappable for object storage later — callers only
/// deal in logical storage keys, never raw filesystem paths.
/// </summary>
/// <remarks>
/// Nothing stored here is expected to survive. snip-it keeps no server-side copy of a visitor's
/// media (#22): the browser holds the video and re-sends it for the cut, so every file written
/// through this interface belongs to exactly one job or one response and is deleted by it. The
/// backing directory is the pod's own ephemeral disk, not a volume, so a restart is a clean slate
/// rather than a loss.
/// </remarks>
public interface IFileStorageService
{
    /// <summary>Saves a stream under a new, unique storage key within the given folder. Returns the storage key.</summary>
    Task<string> SaveAsync(Stream content, string folder, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Resolves a storage key to an absolute filesystem path.</summary>
    string GetFullPath(string storageKey);

    /// <summary>
    /// Opens the stored file for reading <em>once</em>: the file is deleted when the returned
    /// stream is disposed. Returns <c>null</c> if there is nothing at that key.
    /// </summary>
    /// <remarks>
    /// Missing is a normal answer, not a failure: storage keys live in database rows that outlive
    /// the bytes they point at, so callers must be able to say "gone" without an exception. This
    /// deliberately has no companion <c>Exists</c> — checking first and opening second is a race,
    /// so the open itself is the check.
    /// <para>
    /// Delete-on-close rather than a separate <c>Delete</c> call because the caller hands the stream
    /// to ASP.NET and never sees it closed: the response finishes long after the handler returns, so
    /// there is no point in the caller's own code where "the download is done" is observable.
    /// </para>
    /// </remarks>
    Stream? TryOpenReadOnce(string storageKey);

    /// <summary>
    /// Deletes the file at this key if it is still there. Silent when it is already gone —
    /// this runs in <c>finally</c> blocks, where throwing would mask the real failure.
    /// </summary>
    void Delete(string storageKey);
}
