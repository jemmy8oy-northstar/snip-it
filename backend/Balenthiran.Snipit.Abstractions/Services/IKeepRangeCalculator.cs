using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Merges contiguous kept words into keep-ranges for the FFmpeg cut.</summary>
public interface IKeepRangeCalculator
{
    /// <param name="maxPauseSeconds">When set, a gap between two kept words longer than this also splits the run.</param>
    IReadOnlyList<IDomainKeepRange> Calculate(IReadOnlyList<IDomainTranscriptWord> words, double? maxPauseSeconds = null);
}
