using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainCutJob : ICutJob
{
    Guid TranscriptionJobId { get; set; }

    /// <summary>Path (on the configured file storage) to the source video being cut.</summary>
    string SourceFilePath { get; set; }

    /// <summary>Kept word ranges, computed from the submitted cut request. Seconds, in source-video time.</summary>
    IReadOnlyList<IDomainKeepRange> KeepRanges { get; }

    /// <summary>Populated once the job reaches <see cref="JobStatus.Completed"/>.</summary>
    string? OutputFilePath { get; set; }
}
