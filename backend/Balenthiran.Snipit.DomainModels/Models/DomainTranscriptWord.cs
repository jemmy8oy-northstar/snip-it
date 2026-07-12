using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.DomainModels.Models;

public class DomainTranscriptWord : IDomainTranscriptWord
{
    public string Text { get; set; } = string.Empty;
    public double Start { get; set; }
    public double End { get; set; }
    public bool Kept { get; set; } = true;
}
