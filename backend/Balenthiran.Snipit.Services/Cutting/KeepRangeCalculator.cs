using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Cutting;

/// <summary>
/// Merges contiguous kept words (in transcript order) into keep-ranges: a run of consecutive
/// kept words becomes one range spanning from the first word's start to the last word's end,
/// so natural pauses between kept words are preserved. A removed word breaks the run.
/// </summary>
public class KeepRangeCalculator : IKeepRangeCalculator
{
    public List<DomainKeepRange> Calculate(IReadOnlyList<DomainTranscriptWord> words)
    {
        var ranges = new List<DomainKeepRange>();

        double? rangeStart = null;
        double rangeEnd = 0;

        foreach (var word in words)
        {
            if (word.Kept)
            {
                rangeStart ??= word.Start;
                rangeEnd = word.End;
            }
            else if (rangeStart is not null)
            {
                ranges.Add(new DomainKeepRange(rangeStart.Value, rangeEnd));
                rangeStart = null;
            }
        }

        if (rangeStart is not null)
        {
            ranges.Add(new DomainKeepRange(rangeStart.Value, rangeEnd));
        }

        return ranges;
    }
}
