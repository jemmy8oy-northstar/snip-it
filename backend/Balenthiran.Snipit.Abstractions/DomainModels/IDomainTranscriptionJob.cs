using Balenthiran.Snipit.Abstractions.DataModels;

namespace Balenthiran.Snipit.Abstractions.DomainModels;

public interface IDomainTranscriptionJob : ITranscriptionJob
{
    /// <summary>Path (on the configured file storage) to the originally uploaded video/audio file.</summary>
    string SourceFilePath { get; set; }

    /// <summary>Populated once the job reaches <see cref="JobStatus.Completed"/>.</summary>
    IDomainTranscript? Transcript { get; set; }

    bool IsReady { get; }
}
