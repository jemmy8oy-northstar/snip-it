namespace Balenthiran.Snipit.DataModels.Models;

public class Transcript
{
    public Guid TranscriptionJobId { get; set; }
    public double DurationSeconds { get; set; }
    public List<TranscriptSegment> Segments { get; set; } = [];
    public List<TranscriptWord> Words { get; set; } = [];
}
