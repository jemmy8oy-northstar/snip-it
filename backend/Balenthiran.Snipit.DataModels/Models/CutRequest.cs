namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>Submitted to start a cut job: the transcript's words, each marked kept or removed.</summary>
public class CutRequest
{
    public Guid TranscriptionJobId { get; set; }
    public List<TranscriptWord> Words { get; set; } = [];
}
