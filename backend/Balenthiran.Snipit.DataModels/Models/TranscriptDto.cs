namespace Balenthiran.Snipit.DataModels.Models;

public class TranscriptDto
{
    public Guid TranscriptionJobId { get; set; }
    public double DurationSeconds { get; set; }
    public List<TranscriptSegmentDto> Segments { get; set; } = [];
    public List<TranscriptWordDto> Words { get; set; } = [];
}
