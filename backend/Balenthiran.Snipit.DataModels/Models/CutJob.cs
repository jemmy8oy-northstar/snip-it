using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.DataModels.Models;

public class CutJob : ICutJob
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}
