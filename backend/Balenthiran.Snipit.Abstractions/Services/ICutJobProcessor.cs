namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Runs the actual cut pipeline (FFmpeg trim/concat) for one job.</summary>
public interface ICutJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default);
}
