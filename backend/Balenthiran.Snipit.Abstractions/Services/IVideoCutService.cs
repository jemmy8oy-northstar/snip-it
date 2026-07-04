using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Drives FFmpeg to trim + concatenate kept ranges of a single source video into one output MP4.</summary>
public interface IVideoCutService
{
    Task<string> CutAsync(string sourceFilePath, IReadOnlyList<DomainKeepRange> keepRanges, string outputFilePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the FFmpeg argument list for a cut: trim/atrim each keep-range with a short audio
/// fade at every splice, then concat if there is more than one range. Pure — no process execution.
/// Ported from video-templater's cut.py.
/// </summary>
public interface IFfmpegCutArgumentsBuilder
{
    string[] Build(string sourceFilePath, IReadOnlyList<DomainKeepRange> keepRanges, string outputFilePath);
}
