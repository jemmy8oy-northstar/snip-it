using Balenthiran.Snipit.Services.Transcription;

namespace Balenthiran.Snipit.Tests.Transcription;

public class FfmpegAudioExtractionArgumentsBuilderTests
{
    private readonly FfmpegAudioExtractionArgumentsBuilder _sut = new();

    [Fact]
    public void Build_ExtractsMono16kHzPcmWav()
    {
        var args = _sut.Build("in.mp4", "out.wav");

        Assert.Contains("-i", args);
        Assert.Equal("in.mp4", args[Array.IndexOf(args, "-i") + 1]);
        Assert.Contains("-vn", args);
        Assert.Equal("1", args[Array.IndexOf(args, "-ac") + 1]);
        Assert.Equal("16000", args[Array.IndexOf(args, "-ar") + 1]);
        Assert.Equal("pcm_s16le", args[Array.IndexOf(args, "-acodec") + 1]);
        Assert.Equal("out.wav", args[^1]);
    }
}
