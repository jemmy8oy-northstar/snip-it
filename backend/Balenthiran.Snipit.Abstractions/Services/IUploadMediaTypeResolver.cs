namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Resolves the media type to serve an uploaded source file back with. Uploads are not stored with
/// their declared content type, but the storage key keeps the original extension, so the container
/// type is recoverable from the key.
/// </summary>
public interface IUploadMediaTypeResolver
{
    /// <summary>Media type for the given storage key, based on its file extension.</summary>
    string Resolve(string storageKey);
}
