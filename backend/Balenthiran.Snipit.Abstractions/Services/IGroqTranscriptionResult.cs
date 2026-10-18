using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Parsed result of a Groq whisper-large-v3 transcription call.</summary>
public interface IGroqTranscriptionResult
{
    double DurationSeconds { get; }
    IReadOnlyList<IDomainTranscriptSegment> Segments { get; }
    IReadOnlyList<IDomainTranscriptWord> Words { get; }
}
