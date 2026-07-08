namespace Balenthiran.Snipit.Abstractions.DomainModels;

public class DomainTranscriptSegment
{
    public int Index { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}
