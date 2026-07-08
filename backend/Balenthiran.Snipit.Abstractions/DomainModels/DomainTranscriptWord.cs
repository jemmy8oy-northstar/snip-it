namespace Balenthiran.Snipit.Abstractions.DomainModels;

public class DomainTranscriptWord
{
    public string Text { get; set; } = string.Empty;
    public double Start { get; set; }
    public double End { get; set; }
    public bool Kept { get; set; } = true;
}
