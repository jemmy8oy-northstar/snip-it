namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>A single transcript word with second-based timestamps and its kept/removed edit state.</summary>
public class TranscriptWord
{
    public string Text { get; set; } = string.Empty;
    public double Start { get; set; }
    public double End { get; set; }
    public bool Kept { get; set; } = true;
}
