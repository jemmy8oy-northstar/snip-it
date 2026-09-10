namespace Balenthiran.Snipit.Abstractions.DataModels;

public interface ITranscriptionJob
{
    Guid Id { get; set; }
    JobStatus Status { get; set; }
    string? Error { get; set; }
    DateTime CreatedAt { get; set; }
}
