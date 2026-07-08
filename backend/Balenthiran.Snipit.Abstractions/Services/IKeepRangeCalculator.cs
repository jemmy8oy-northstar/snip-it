using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Merges contiguous kept words into keep-ranges for the FFmpeg cut.</summary>
public interface IKeepRangeCalculator
{
    List<DomainKeepRange> Calculate(IReadOnlyList<DomainTranscriptWord> words);
}
