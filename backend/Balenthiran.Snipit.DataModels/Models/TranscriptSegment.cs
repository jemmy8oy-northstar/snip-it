namespace Balenthiran.Snipit.DataModels.Models;

public class TranscriptSegment
{
    public required int Index { get; set; }
    public required double Start { get; set; }
    public required double End { get; set; }
    public required string Text { get; set; }
}
