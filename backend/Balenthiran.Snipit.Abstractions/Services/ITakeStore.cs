using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Takes a creator chose to save (BEH-EDITION-3), kept beside the production they came from on a
/// volume that belongs to snip-it's server. Separate from <see cref="IFileStorageService"/> on
/// purpose: that is delete-after-use scratch (#22), this is the one place snip-it keeps media,
/// and only because the creator pressed Save.
/// </summary>
public interface ITakeStore
{
    /// <summary>False when no directory is configured: saving is switched off and every route says so.</summary>
    bool Enabled { get; }

    /// <summary>True for a name that may be stored: letters, digits, dot, dash, underscore; not hidden; at most 100 characters.</summary>
    bool IsValidName(string name);

    /// <summary>
    /// Saves a take under its production, replacing a take of the same name (saving twice is not an
    /// error). Returns null, storing nothing, when it would push the stored total past the cap.
    /// </summary>
    Task<ISavedTake?> SaveAsync(Guid productionId, string name, Stream content, CancellationToken cancellationToken = default);

    IReadOnlyList<ISavedTake> List(Guid productionId);

    /// <summary>Opens a saved take for reading, or null when there is none by that name.</summary>
    Stream? TryOpen(Guid productionId, string name);
}
