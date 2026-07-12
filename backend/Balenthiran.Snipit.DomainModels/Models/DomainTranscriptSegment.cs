using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.DomainModels.Models;

public class DomainTranscriptSegment : IDomainTranscriptSegment
{
    public int Index { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}
