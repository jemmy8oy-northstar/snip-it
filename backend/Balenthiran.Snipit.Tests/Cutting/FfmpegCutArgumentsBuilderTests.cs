using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Services.Cutting;

namespace Balenthiran.Snipit.Tests.Cutting;

/// <summary>
/// Verifies the FFmpeg argument construction matches video-templater's cut.py exactly:
/// single-range trim+afade, multi-range trim+afade+concat, 20ms splice fades, libx264/aac output.
/// </summary>
public class FfmpegCutArgumentsBuilderTests
{
    private readonly FfmpegCutArgumentsBuilder _sut = new();

    [Fact]
    public void Build_ThrowsWhenNoKeepRanges()
    {
        Assert.Throws<ArgumentException>(() => _sut.Build("in.mp4", [], "out.mp4"));
    }

    [Fact]
    public void Build_SingleRange_UsesTrimNotConcat()
    {
        var ranges = new[] { new DomainKeepRange(0.5, 3.2) };

        var args = _sut.Build("in.mp4", ranges, "out.mp4");
        var filterComplex = GetArg(args, "-filter_complex");

        Assert.Contains("[0:v]trim=start=0.5:end=3.2,setpts=PTS-STARTPTS[outv]", filterComplex);
        Assert.Contains("[0:a]atrim=start=0.5:end=3.2,asetpts=PTS-STARTPTS", filterComplex);
        Assert.Contains("afade=t=in:st=0:d=0.0200", filterComplex);
        // duration 2.7s, fade-out starts at 2.7 - 0.02 = 2.68
        Assert.Contains("afade=t=out:st=2.6800:d=0.0200[outa]", filterComplex);
        Assert.DoesNotContain("concat", filterComplex);
    }

    [Fact]
    public void Build_MultipleRanges_ConcatenatesInOrder()
    {
        var ranges = new[]
        {
            new DomainKeepRange(0.0, 3.2),
            new DomainKeepRange(6.0, 8.3),
        };

        var args = _sut.Build("in.mp4", ranges, "out.mp4");
        var filterComplex = GetArg(args, "-filter_complex");

        Assert.Contains("[0:v]trim=start=0:end=3.2,setpts=PTS-STARTPTS[v0]", filterComplex);
        Assert.Contains("[0:v]trim=start=6:end=8.3,setpts=PTS-STARTPTS[v1]", filterComplex);
        Assert.Contains("[v0][a0][v1][a1]concat=n=2:v=1:a=1[outv][outa]", filterComplex);
    }

    [Fact]
    public void Build_UsesSingleInputForTheOneSourceFile()
    {
        var ranges = new[]
        {
            new DomainKeepRange(0.0, 1.0),
            new DomainKeepRange(2.0, 3.0),
        };

        var args = _sut.Build("in.mp4", ranges, "out.mp4");

        // Only one -i, regardless of how many keep-ranges reference it (matches cut.py's
        // dedupe-by-path behaviour, specialised to the single-source-file case).
        Assert.Equal(1, args.Count(a => a == "-i"));
        Assert.Equal("in.mp4", GetArg(args, "-i"));
    }

    [Fact]
    public void Build_SetsExpectedOutputEncoding()
    {
        var ranges = new[] { new DomainKeepRange(0, 1) };

        var args = _sut.Build("in.mp4", ranges, "out.mp4");

        Assert.Equal("libx264", GetArg(args, "-c:v"));
        Assert.Equal("18", GetArg(args, "-crf"));
        Assert.Equal("fast", GetArg(args, "-preset"));
        Assert.Equal("aac", GetArg(args, "-c:a"));
        Assert.Equal("192k", GetArg(args, "-b:a"));
        Assert.Equal("out.mp4", args[^1]);
        Assert.Equal("-y", args[^2]);
    }

    private static string GetArg(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        Assert.True(index >= 0, $"Flag {flag} not found in args: {string.Join(' ', args)}");
        return args[index + 1];
    }
}
