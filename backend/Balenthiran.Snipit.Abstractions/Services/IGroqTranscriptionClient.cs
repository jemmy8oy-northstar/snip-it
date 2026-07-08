namespace Balenthiran.Snipit.Abstractions.Services;

public interface IGroqTranscriptionClient
{
    Task<GroqTranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);
}
