using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.EntityModels;

public class CutJobEntity
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid TranscriptionJobId { get; set; }

    /// <summary>Storage key for the source video being cut.</summary>
    public string SourceFilePath { get; set; } = string.Empty;

    /// <summary>Serialized list of <see cref="Balenthiran.Snipit.Abstractions.DomainModels.DomainKeepRange"/>.</summary>
    public string KeepRangesJson { get; set; } = "[]";

    /// <summary>Storage key for the produced MP4, null until Completed.</summary>
    public string? OutputFilePath { get; set; }
}
