namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>Submitted to start a cut job: the transcript's words, each marked kept or removed.</summary>
public class CutRequest
{
    public required Guid TranscriptionJobId { get; set; }
    public required List<TranscriptWord> Words { get; set; }
}
