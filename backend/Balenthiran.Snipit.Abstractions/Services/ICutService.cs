using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Handles the submit / poll / download lifecycle for cut jobs.
/// The actual FFmpeg cutting work happens out of band — see <see cref="ICutJobProcessor"/>.
/// </summary>
public interface ICutService
{
    /// <param name="sourceStorageKey">
    /// Scratch key for the video this cut runs against, uploaded with the request. It is not taken
    /// from the transcription job any more: snip-it keeps no server-side copy between requests
    /// (#22), so by the time a cut is submitted the transcribed file is long gone and the browser
    /// — which still holds the file the visitor picked — sends it again.
    /// </param>
    Task<IDomainCutJob> SubmitAsync(Guid transcriptionJobId, string sourceStorageKey, IReadOnlyList<IDomainTranscriptWord> words, CancellationToken cancellationToken = default);
    Task<IDomainCutJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
