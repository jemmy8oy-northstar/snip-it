using System.Threading.Channels;
using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Infrastructure;

public class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

    public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        if (!_channel.Writer.TryWrite(workItem))
        {
            throw new InvalidOperationException("Failed to enqueue background job.");
        }
    }

    public Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken).AsTask();
}
