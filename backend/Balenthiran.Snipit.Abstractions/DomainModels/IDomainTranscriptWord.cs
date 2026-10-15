namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainTranscriptWord
{
    string Text { get; set; }
    double Start { get; set; }
    double End { get; set; }
    bool Kept { get; set; }
}
