using System.Globalization;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Cutting;

/// <summary>
/// Direct port of video-templater's cut.py: trims each keep-range from the single source video,
/// applies a 20ms audio fade at every splice to kill clicks, then concatenates all ranges into
/// one output. Single-input specialisation of cut.py (which dedupes multiple source files down to
/// one -i per file) — here there is always exactly one source file, so there is always one -i.
/// </summary>
public class FfmpegCutArgumentsBuilder : IFfmpegCutArgumentsBuilder
{
    private const double FadeSeconds = 0.02;

    public string[] Build(string sourceFilePath, IReadOnlyList<DomainKeepRange> keepRanges, string outputFilePath)
    {
        if (keepRanges.Count == 0)
        {
            throw new ArgumentException("No kept segments — nothing to cut.", nameof(keepRanges));
        }

        var filterComplex = keepRanges.Count == 1
            ? BuildSingleRangeFilter(keepRanges[0])
            : BuildMultiRangeFilter(keepRanges);

        return
        [
            "-hide_banner",
            "-i", sourceFilePath,
            "-filter_complex", filterComplex,
            "-map", "[outv]", "-map", "[outa]",
            "-c:v", "libx264", "-crf", "18", "-preset", "fast",
            "-c:a", "aac", "-b:a", "192k",
            "-y", outputFilePath,
        ];
    }

    private static string BuildSingleRangeFilter(DomainKeepRange range)
    {
        var duration = range.End - range.Start;
        var fadeOutStart = FormatFixed(duration - FadeSeconds);

        return $"[0:v]trim=start={FormatSeconds(range.Start)}:end={FormatSeconds(range.End)},setpts=PTS-STARTPTS[outv];" +
               $"[0:a]atrim=start={FormatSeconds(range.Start)}:end={FormatSeconds(range.End)},asetpts=PTS-STARTPTS," +
               $"afade=t=in:st=0:d={FormatFixed(FadeSeconds)},afade=t=out:st={fadeOutStart}:d={FormatFixed(FadeSeconds)}[outa]";
    }

    private static string BuildMultiRangeFilter(IReadOnlyList<DomainKeepRange> ranges)
    {
        var parts = new List<string>();
        var concatStreams = "";

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            var duration = range.End - range.Start;
            var fadeOutStart = FormatFixed(duration - FadeSeconds);

            parts.Add($"[0:v]trim=start={FormatSeconds(range.Start)}:end={FormatSeconds(range.End)},setpts=PTS-STARTPTS[v{i}]");
            parts.Add($"[0:a]atrim=start={FormatSeconds(range.Start)}:end={FormatSeconds(range.End)},asetpts=PTS-STARTPTS," +
                      $"afade=t=in:st=0:d={FormatFixed(FadeSeconds)},afade=t=out:st={fadeOutStart}:d={FormatFixed(FadeSeconds)}[a{i}]");
            concatStreams += $"[v{i}][a{i}]";
        }

        return string.Join(";", parts) + ";" + concatStreams + $"concat=n={ranges.Count}:v=1:a=1[outv][outa]";
    }

    private static string FormatSeconds(double seconds) => seconds.ToString(CultureInfo.InvariantCulture);

    private static string FormatFixed(double seconds) => seconds.ToString("F4", CultureInfo.InvariantCulture);
}
