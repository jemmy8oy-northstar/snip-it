using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.Services.Cutting;
using Balenthiran.Snipit.Services.Infrastructure;
using Moq;

namespace Balenthiran.Snipit.Tests.Cutting;

public class VideoCutServiceTests
{
    private readonly Mock<IFfmpegCutArgumentsBuilder> _argumentsBuilder = new();
    private readonly Mock<IProcessRunner> _processRunner = new();

    private VideoCutService CreateSut() => new(_argumentsBuilder.Object, _processRunner.Object);

    [Fact]
    public async Task CutAsync_RunsFfmpegWithBuiltArgumentsAndReturnsOutputPath()
    {
        var ranges = new List<DomainKeepRange> { new(0, 1) };
        var builtArgs = new[] { "-i", "in.mp4", "out.mp4" };
        _argumentsBuilder.Setup(x => x.Build("in.mp4", ranges, "out.mp4")).Returns(builtArgs);
        _processRunner.Setup(x => x.RunAsync("ffmpeg", builtArgs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var result = await CreateSut().CutAsync("in.mp4", ranges, "out.mp4");

        Assert.Equal("out.mp4", result);
        _processRunner.Verify(x => x.RunAsync("ffmpeg", builtArgs, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CutAsync_ThrowsWhenFfmpegExitsNonZero()
    {
        var ranges = new List<DomainKeepRange> { new(0, 1) };
        _argumentsBuilder.Setup(x => x.Build(It.IsAny<string>(), ranges, It.IsAny<string>())).Returns(["-i", "in.mp4"]);
        _processRunner.Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "boom"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().CutAsync("in.mp4", ranges, "out.mp4"));
        Assert.Contains("boom", ex.Message);
    }
}
