namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainTranscriptSegment
{
    int Index { get; set; }
    double Start { get; set; }
    double End { get; set; }
    string Text { get; set; }
}
