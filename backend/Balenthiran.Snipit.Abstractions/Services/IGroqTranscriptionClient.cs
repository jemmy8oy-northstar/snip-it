using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

public interface IGroqTranscriptionClient
{
    Task<GroqTranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Parsed result of a Groq whisper-large-v3 transcription call.</summary>
public class GroqTranscriptionResult
{
    public double DurationSeconds { get; set; }
    public List<DomainTranscriptSegment> Segments { get; set; } = [];
    public List<DomainTranscriptWord> Words { get; set; } = [];
}
