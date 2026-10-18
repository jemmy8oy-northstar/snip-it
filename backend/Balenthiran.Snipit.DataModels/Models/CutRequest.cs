namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>Submitted to start a cut job: the transcript's words, each marked kept or removed.</summary>
public class CutRequest
{
    public required Guid TranscriptionJobId { get; set; }
    public required List<TranscriptWord> Words { get; set; }

    /// <summary>
    /// Optional. A silence between two kept words longer than this is cut out too — one-click
    /// dead-air removal. Unset keeps every pause, which is what a cut did before this existed.
    /// </summary>
    public double? MaxPauseSeconds { get; set; }
}
