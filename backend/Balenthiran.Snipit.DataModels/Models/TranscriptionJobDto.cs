using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.DataModels.Models;

public class TranscriptionJobDto : ITranscriptionJob
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}
