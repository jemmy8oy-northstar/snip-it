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
        var storageKey = $"{folder}/{Guid.NewGuid():N}_{fileName}";
        var fullPath = GetFullPath(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public string GetFullPath(string storageKey) => Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));

    public Stream OpenRead(string storageKey) => File.OpenRead(GetFullPath(storageKey));
}
