using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.DomainModels.Models;

public class DomainTranscript : IDomainTranscript
{
    public double DurationSeconds { get; set; }
    public List<DomainTranscriptSegment> Segments { get; set; } = [];
    public List<DomainTranscriptWord> Words { get; set; } = [];

    IReadOnlyList<IDomainTranscriptSegment> IDomainTranscript.Segments => Segments;
    IReadOnlyList<IDomainTranscriptWord> IDomainTranscript.Words => Words;
}
