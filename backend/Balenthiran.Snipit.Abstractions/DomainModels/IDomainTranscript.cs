namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainTranscript
{
    double DurationSeconds { get; set; }
    List<DomainTranscriptSegment> Segments { get; set; }
    List<DomainTranscriptWord> Words { get; set; }
}
