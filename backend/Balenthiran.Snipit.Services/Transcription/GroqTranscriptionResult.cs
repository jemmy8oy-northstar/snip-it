using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DomainModels.Models;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>Parsed result of a Groq whisper-large-v3 transcription call. Concrete carrier for
/// <see cref="IGroqTranscriptionResult"/>; the explicit-interface members expose the concrete
/// segment/word lists covariantly so the contract only ever hands out the interface shape.</summary>
public class GroqTranscriptionResult : IGroqTranscriptionResult
{
    public double DurationSeconds { get; set; }
    public List<DomainTranscriptSegment> Segments { get; set; } = [];
    public List<DomainTranscriptWord> Words { get; set; } = [];

    IReadOnlyList<IDomainTranscriptSegment> IGroqTranscriptionResult.Segments => Segments;
    IReadOnlyList<IDomainTranscriptWord> IGroqTranscriptionResult.Words => Words;
}
