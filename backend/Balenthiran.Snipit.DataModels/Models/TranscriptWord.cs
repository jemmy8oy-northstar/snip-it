namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>A single transcript word with second-based timestamps and its kept/removed edit state.</summary>
public class TranscriptWord
{
    public required string Text { get; set; }
    public required double Start { get; set; }
    public required double End { get; set; }

    /// <summary>Whether the word survives the cut. Always true on a freshly transcribed word;
    /// the editor flips it and sends the whole list back in a <see cref="CutRequest"/>.</summary>
    public required bool Kept { get; set; }
}
