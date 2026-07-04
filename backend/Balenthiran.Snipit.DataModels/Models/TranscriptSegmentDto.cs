namespace Balenthiran.Snipit.DataModels.Models;

public class TranscriptSegmentDto
{
    public int Index { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}
