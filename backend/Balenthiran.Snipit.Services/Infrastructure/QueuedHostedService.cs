using Balenthiran.Snipit.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balenthiran.Snipit.Services.Infrastructure;

/// <summary>
/// Single-worker background job runner. Dequeues one job at a time and executes it with a
/// fresh DI scope — enough concurrency for v1's transcription/cut workload without a real queue.
/// </summary>
public class QueuedHostedService(
    IBackgroundJobQueue queue,
    IServiceProvider serviceProvider,
    ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await queue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = serviceProvider.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Background job failed.");
            }
        }
    }
}
