using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Handles the submit / poll / download lifecycle for cut jobs.
/// The actual FFmpeg cutting work happens out of band — see <see cref="ICutJobProcessor"/>.
/// </summary>
public interface ICutService
{
    Task<IDomainCutJob> SubmitAsync(Guid transcriptionJobId, List<DomainTranscriptWord> words, CancellationToken cancellationToken = default);
    Task<IDomainCutJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>Runs the actual cut pipeline (FFmpeg trim/concat) for one job.</summary>
public interface ICutJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>Merges contiguous kept words into keep-ranges for the FFmpeg cut.</summary>
public interface IKeepRangeCalculator
{
    List<DomainKeepRange> Calculate(IReadOnlyList<DomainTranscriptWord> words);
}
