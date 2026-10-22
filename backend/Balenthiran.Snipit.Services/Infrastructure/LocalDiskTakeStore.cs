using System.Text.RegularExpressions;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Infrastructure;

/// <summary>
/// Saved takes as plain files: <c>{root}/{productionId}/{name}</c>. The directory is the whole
/// database, as it is on the phone. Both path segments are validated rather than sanitised — a
/// production id is a <see cref="Guid"/> and a name matches a whitelist — so nothing a client
/// sends can name a path outside the root.
/// </summary>
public partial class LocalDiskTakeStore(IOptions<TakeStoreOptions> options) : ITakeStore
{
    private const int MaxNameLength = 100;
    private readonly string _root = options.Value.RootPath;
    private readonly long _maxTotalBytes = options.Value.MaxTotalBytes;

    public bool Enabled => !string.IsNullOrWhiteSpace(_root);

    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]*$")]
    private static partial Regex NamePattern();

    public bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxNameLength && NamePattern().IsMatch(name) && !name.Contains("..");

    public async Task<ISavedTake?> SaveAsync(Guid productionId, string name, Stream content, CancellationToken cancellationToken = default)
    {
        var path = PathOf(productionId, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside the target and moved into place, so a dropped connection never leaves a
        // truncated file that List would offer as a take. The leading dot keeps it out of List
        // and out of the name whitelist.
        var partial = Path.Combine(Path.GetDirectoryName(path)!, $".{Guid.NewGuid():N}.part");
        try
        {
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            // Checked after the write, against what actually landed rather than a client-declared
            // length. A take being replaced does not count against itself.
            var replaced = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (TotalBytes() - replaced > _maxTotalBytes)
            {
                return null;
            }

            File.Move(partial, path, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }

        return Describe(new FileInfo(path));
    }

    public IReadOnlyList<ISavedTake> List(Guid productionId)
    {
        var directory = DirectoryOf(productionId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return new DirectoryInfo(directory).EnumerateFiles()
            .Where(f => IsValidName(f.Name))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(Describe)
            .ToList();
    }

    public Stream? TryOpen(Guid productionId, string name)
    {
        try
        {
            return new FileStream(PathOf(productionId, name), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Everything under the root, the in-flight upload included (it is already on disk when this runs).</summary>
    private long TotalBytes() =>
        Directory.Exists(_root)
            ? new DirectoryInfo(_root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
            : 0;

    private string DirectoryOf(Guid productionId) => Path.Combine(Path.GetFullPath(_root), productionId.ToString("D"));

    private string PathOf(Guid productionId, string name) =>
        IsValidName(name)
            ? Path.Combine(DirectoryOf(productionId), name)
            : throw new ArgumentException($"'{name}' is not a valid take name.", nameof(name));

    private static ISavedTake Describe(FileInfo file) => new StoredTake(file.Name, file.Length, file.LastWriteTimeUtc);

    private sealed record StoredTake(string Name, long Bytes, DateTime SavedAt) : ISavedTake;
}
