using Balenthiran.Snipit.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Infrastructure;

/// <summary>
/// Stores files on local disk under a scratch root. Storage keys are "{folder}/{guid}_{fileName}"
/// relative paths — never raw absolute filesystem paths — so this can be swapped for object storage later.
/// </summary>
/// <remarks>
/// The root defaults to a directory under the OS temp path rather than the application directory,
/// because in the cluster there is no volume behind it (#22): it is the container's own writable
/// layer, and everything in it is meant to be short-lived scratch for one job or one response.
/// </remarks>
public class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalDiskFileStorageService(IOptions<FileStorageOptions> options)
    {
        _rootPath = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? Path.Combine(Path.GetTempPath(), "snipit-scratch")
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

    public Stream? TryOpenReadOnce(string storageKey)
    {
        // Both are "not there": the file can go while its folder stays (one deletion), and the
        // folder can go while the key stays (an empty scratch root behind a database that survived
        // a restart). A key that escapes the storage root is NOT this — GetFullPath still throws,
        // because that is a broken caller or an attack, and answering "404" would hide it.
        try
        {
            // DeleteOnClose does the deleting, rather than the caller: the file is unlinked when
            // this stream is disposed, which for a response body is after the last byte has been
            // written. FileShare.Delete is required alongside it on Windows, where an open handle
            // otherwise blocks the unlink; it is harmless elsewhere.
            return new FileStream(
                GetFullPath(storageKey),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);
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

    public void Delete(string storageKey)
    {
        // GetFullPath still throws for a key escaping the root — deleting by an unvalidated key is
        // the one operation where "be forgiving" would be actively dangerous.
        var fullPath = GetFullPath(storageKey);

        try
        {
            File.Delete(fullPath);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone. File.Delete is itself silent about a missing *file*, but not about a
            // missing folder, and both mean the same thing to every caller here.
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
