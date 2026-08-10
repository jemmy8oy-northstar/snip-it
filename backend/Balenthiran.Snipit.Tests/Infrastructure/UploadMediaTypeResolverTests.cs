using Balenthiran.Snipit.Services.Infrastructure;

namespace Balenthiran.Snipit.Tests.Infrastructure;

public class UploadMediaTypeResolverTests
{
    private readonly UploadMediaTypeResolver _resolver = new();

    [Theory]
    [InlineData("uploads/abc_clip.mp4", "video/mp4")]
    [InlineData("uploads/abc_clip.webm", "video/webm")]
    [InlineData("uploads/abc_clip.mov", "video/quicktime")]
    [InlineData("uploads/abc_voice.mp3", "audio/mpeg")]
    [InlineData("uploads/abc_voice.wav", "audio/wav")]
    public void Resolve_maps_known_extensions(string storageKey, string expected)
    {
        Assert.Equal(expected, _resolver.Resolve(storageKey));
    }

    [Theory]
    [InlineData("uploads/abc_CLIP.MP4")]
    [InlineData("uploads/abc_clip.MoV")]
    public void Resolve_is_case_insensitive(string storageKey)
    {
        Assert.StartsWith("video/", _resolver.Resolve(storageKey));
    }

    // A wrong-but-playable type beats octet-stream: the editor scrubs the source in a <video>
    // element, which refuses to play an octet-stream response at all.
    [Theory]
    [InlineData("uploads/abc_clip.xyz")]
    [InlineData("uploads/abc_noextension")]
    [InlineData("")]
    public void Resolve_falls_back_to_mp4_for_unknown_keys(string storageKey)
    {
        Assert.Equal("video/mp4", _resolver.Resolve(storageKey));
    }
}
