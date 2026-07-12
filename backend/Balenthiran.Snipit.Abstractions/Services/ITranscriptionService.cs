using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// Handles the submit / poll / fetch lifecycle for transcription jobs.
/// The actual audio extraction + transcription work happens out of band —
/// see <see cref="ITranscriptionJobProcessor"/>.
/// </summary>
public interface ITranscriptionService
{
    Task<IDomainTranscriptionJob> SubmitAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task<IDomainTranscriptionJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
