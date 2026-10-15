using Balenthiran.Snipit.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Infrastructure;

/// <summary>
/// Stores files on local disk under a configured root. Storage keys are "{folder}/{guid}_{fileName}"
/// relative paths — never raw absolute filesystem paths — so this can be swapped for object storage later.
/// </summary>
public class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalDiskFileStorageService(IOptions<FileStorageOptions> options)
    {
        _rootPath = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "storage")
            : options.Value.RootPath;
    }

    public async Task<string> SaveAsync(Stream content, string folder, string fileName, CancellationToken cancellationToken = default)
    {
        var storageKey = $"{folder}/{Guid.NewGuid():N}_{SanitizeFileName(fileName)}";
        var fullPath = GetFullPath(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public string GetFullPath(string storageKey)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_rootPath));
        var fullPath = Path.GetFullPath(Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar)));

        // Storage keys embed client input (upload filenames) and round-trip through the
        // database; refuse any key that resolves outside the storage root.
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Storage key '{storageKey}' resolves outside the storage root.");
        }

        return fullPath;
    }

    public Stream? TryOpenRead(string storageKey)
    {
        // Both are "not there": the file can go while its folder stays (one deletion), and the
        // folder can go while the key stays (an empty volume behind a database that survived).
        // A key that escapes the storage root is NOT this — GetFullPath still throws, because that
        // is a broken caller or an attack, and answering "404" would hide it.
        try
        {
            return File.OpenRead(GetFullPath(storageKey));
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

    /// <summary>
    /// Client-supplied upload names must not influence the storage path: drop directory
    /// components, then whitelist to letters/digits/dot/dash/underscore.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray()).Trim('.');
        return cleaned.Length == 0 ? "upload" : cleaned;
    }
}
