using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Services.Cutting;

namespace Balenthiran.Snipit.Tests.Cutting;

public class KeepRangeCalculatorTests
{
    private readonly KeepRangeCalculator _sut = new();

    private static DomainTranscriptWord Word(double start, double end, bool kept) =>
        new() { Text = "w", Start = start, End = end, Kept = kept };

    [Fact]
    public void Calculate_AllWordsKept_ReturnsOneRangeSpanningEverything()
    {
        var words = new[] { Word(0, 1, true), Word(1, 2, true), Word(2, 3, true) };

        var ranges = _sut.Calculate(words);

        Assert.Single(ranges);
        Assert.Equal(new DomainKeepRange(0, 3), ranges[0]);
    }

    [Fact]
    public void Calculate_RemovedWordSplitsRuns()
    {
        var words = new[]
        {
            Word(0, 1, true),
            Word(1, 2, true),
            Word(2, 3, false), // removed — splits the run
            Word(3, 4, true),
        };

        var ranges = _sut.Calculate(words);

        Assert.Equal(2, ranges.Count);
        Assert.Equal(new DomainKeepRange(0, 2), ranges[0]);
        Assert.Equal(new DomainKeepRange(3, 4), ranges[1]);
    }

    [Fact]
    public void Calculate_AllWordsRemoved_ReturnsEmpty()
    {
        var words = new[] { Word(0, 1, false), Word(1, 2, false) };

        var ranges = _sut.Calculate(words);

        Assert.Empty(ranges);
    }

    [Fact]
    public void Calculate_TrailingRemovedWord_ClosesFinalRange()
    {
        var words = new[] { Word(0, 1, true), Word(1, 2, false) };

        var ranges = _sut.Calculate(words);

        Assert.Single(ranges);
        Assert.Equal(new DomainKeepRange(0, 1), ranges[0]);
    }

    [Fact]
    public void Calculate_NoWords_ReturnsEmpty()
    {
        var ranges = _sut.Calculate([]);

        Assert.Empty(ranges);
    }
}
