using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.DataModels.Models;

public class CutJob : ICutJob
{
    public required Guid Id { get; set; }
    public required JobStatus Status { get; set; }

    /// <summary>Always present in the payload, null unless the job failed — so a client sees
    /// `string | null` rather than an optional property.</summary>
    public required string? Error { get; set; }
    public required DateTime CreatedAt { get; set; }
}
