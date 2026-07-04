using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Services.Transcription;
using Moq;

namespace Balenthiran.Snipit.Tests.Transcription;

public class AudioExtractionServiceTests
{
    private readonly Mock<IFfmpegAudioExtractionArgumentsBuilder> _argumentsBuilder = new();
    private readonly Mock<IProcessRunner> _processRunner = new();

    private AudioExtractionService CreateSut() => new(_argumentsBuilder.Object, _processRunner.Object);

    [Fact]
    public async Task ExtractAudioAsync_RunsFfmpegAndReturnsOutputPath()
    {
        var builtArgs = new[] { "-i", "in.mp4", "out.wav" };
        _argumentsBuilder.Setup(x => x.Build("in.mp4", "out.wav")).Returns(builtArgs);
        _processRunner.Setup(x => x.RunAsync("ffmpeg", builtArgs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var result = await CreateSut().ExtractAudioAsync("in.mp4", "out.wav");

        Assert.Equal("out.wav", result);
    }

    [Fact]
    public async Task ExtractAudioAsync_ThrowsWhenFfmpegExitsNonZero()
    {
        _argumentsBuilder.Setup(x => x.Build(It.IsAny<string>(), It.IsAny<string>())).Returns(["-i", "in.mp4"]);
        _processRunner.Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "no such file"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ExtractAudioAsync("in.mp4", "out.wav"));
        Assert.Contains("no such file", ex.Message);
    }
}
