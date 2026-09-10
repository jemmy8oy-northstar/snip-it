namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// In-process, single-worker async job queue. Enough for v1's "submit → poll → fetch" jobs
/// without standing up external queue infrastructure.
/// </summary>
public interface IBackgroundJobQueue
{
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);
    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
