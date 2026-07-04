using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.EntityModels;

public class TranscriptionJobEntity
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Storage key for the originally uploaded video/audio file.</summary>
    public string SourceFilePath { get; set; } = string.Empty;

    /// <summary>Serialized <see cref="Balenthiran.Snipit.DomainModels.Models.DomainTranscript"/>, null until Completed.</summary>
    public string? TranscriptJson { get; set; }
}
