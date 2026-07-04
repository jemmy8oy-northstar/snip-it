namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>Submitted to start a cut job: the transcript's words, each marked kept or removed.</summary>
public class CutRequestDto
{
    public Guid TranscriptionJobId { get; set; }
    public List<TranscriptWordDto> Words { get; set; } = [];
}
