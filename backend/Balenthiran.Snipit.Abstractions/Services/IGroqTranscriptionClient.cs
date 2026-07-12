namespace Balenthiran.Snipit.Abstractions.Services;

public interface IGroqTranscriptionClient
{
    Task<IGroqTranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);
}
