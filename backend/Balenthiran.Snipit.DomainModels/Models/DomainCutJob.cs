using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DataModels.Models;

namespace Balenthiran.Snipit.DomainModels.Models;

public class DomainCutJob : CutJob, IDomainCutJob
{
    public Guid TranscriptionJobId { get; set; }
    public string SourceFilePath { get; set; } = string.Empty;
    public List<DomainKeepRange> KeepRanges { get; set; } = [];
    public string? OutputFilePath { get; set; }

    IReadOnlyList<IDomainKeepRange> IDomainCutJob.KeepRanges => KeepRanges;
}
