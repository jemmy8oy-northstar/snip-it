using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Drives FFmpeg to trim + concatenate kept ranges of a single source video into one output MP4.</summary>
public interface IVideoCutService
{
    Task<string> CutAsync(string sourceFilePath, IReadOnlyList<IDomainKeepRange> keepRanges, string outputFilePath, CancellationToken cancellationToken = default);
}
