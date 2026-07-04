namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainTranscript
{
    double DurationSeconds { get; set; }
    List<DomainTranscriptSegment> Segments { get; set; }
    List<DomainTranscriptWord> Words { get; set; }
}

public class DomainTranscriptSegment
{
    public int Index { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class DomainTranscriptWord
{
    public string Text { get; set; } = string.Empty;
    public double Start { get; set; }
    public double End { get; set; }
    public bool Kept { get; set; } = true;
}
