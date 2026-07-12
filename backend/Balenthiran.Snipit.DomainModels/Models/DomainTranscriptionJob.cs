using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DataModels.Models;

namespace Balenthiran.Snipit.DomainModels.Models;

public class DomainTranscriptionJob : TranscriptionJob, IDomainTranscriptionJob
{
    public string SourceFilePath { get; set; } = string.Empty;
    public IDomainTranscript? Transcript { get; set; }

    public bool IsReady => Status == JobStatus.Completed && Transcript is not null;
}
