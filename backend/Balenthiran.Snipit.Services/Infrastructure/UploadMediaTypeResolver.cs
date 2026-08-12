using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Infrastructure;

/// <inheritdoc cref="IUploadMediaTypeResolver" />
public class UploadMediaTypeResolver : IUploadMediaTypeResolver
{
    /// <summary>
    /// Unknown extensions fall back to mp4 rather than octet-stream: the editor plays the source in a
    /// <c>&lt;video&gt;</c> element, and octet-stream stops playback outright, whereas browsers sniff
    /// past a merely-wrong video type.
    /// </summary>
    private const string FallbackMediaType = "video/mp4";

    private static readonly Dictionary<string, string> MediaTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".webm"] = "video/webm",
        [".ogv"] = "video/ogg",
        [".mov"] = "video/quicktime",
        [".mkv"] = "video/x-matroska",
        [".avi"] = "video/x-msvideo",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".flac"] = "audio/flac",
    };

    public string Resolve(string storageKey)
    {
        var extension = Path.GetExtension(storageKey ?? string.Empty);
        return MediaTypesByExtension.TryGetValue(extension, out var mediaType) ? mediaType : FallbackMediaType;
    }
}
