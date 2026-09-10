namespace Balenthiran.Snipit.DataModels.Models;

public class Transcript
{
    public required Guid TranscriptionJobId { get; set; }
    public required double DurationSeconds { get; set; }
    public required List<TranscriptSegment> Segments { get; set; }
    public required List<TranscriptWord> Words { get; set; }
}
