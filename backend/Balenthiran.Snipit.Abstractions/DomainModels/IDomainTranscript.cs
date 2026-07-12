namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainTranscript
{
    double DurationSeconds { get; set; }
    IReadOnlyList<IDomainTranscriptSegment> Segments { get; }
    IReadOnlyList<IDomainTranscriptWord> Words { get; }
}
