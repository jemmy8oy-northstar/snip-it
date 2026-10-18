namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Runs the actual transcription pipeline (audio extraction + Groq call) for one job.</summary>
public interface ITranscriptionJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default);
}
