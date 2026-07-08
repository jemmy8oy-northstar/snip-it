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
