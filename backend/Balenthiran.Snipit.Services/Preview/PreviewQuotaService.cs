using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Preview;

/// <summary>
/// Two independent brakes: a daily count taken from the jobs table, and a short cooldown set
/// when the provider rate limits us. The count is durable because it is derived from rows that
/// already exist — no new column, no migration, and a restart cannot reset it to zero. The
/// cooldown is deliberately in-memory: it is a cached fact about the outside world, and losing
/// it on restart only means the next upload rediscovers it, which is the safe direction.
/// </summary>
/// <remarks>
/// Registered as a singleton so the cooldown outlives a request, which is why the daily count
/// is read through a scope factory rather than an injected <see cref="AppDbContext"/> — a
/// singleton holding a scoped DbContext would be shared across concurrent requests.
/// </remarks>
public class PreviewQuotaService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<PreviewOptions> options) : IPreviewQuotaService
{
    private readonly PreviewOptions _options = options.Value;
    private readonly Lock _gate = new();
    private DateTimeOffset? _blockedUntilUtc;

    public async Task<string?> GetBlockReasonAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset? blockedUntil;
        lock (_gate)
        {
            blockedUntil = _blockedUntilUtc;
        }

        if (blockedUntil is not null && blockedUntil > timeProvider.GetUtcNow())
        {
            return _options.UpstreamLimitMessage;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
        if (dbContext is null)
        {
            // No connection string configured — the app cannot store a job either, so there is
            // nothing here to protect. Let the request through and fail on its own terms.
            return null;
        }

        var startOfDayUtc = DateTime.SpecifyKind(timeProvider.GetUtcNow().UtcDateTime.Date, DateTimeKind.Utc);
        var todaysJobs = await dbContext.TranscriptionJobs
            .AsNoTracking()
            .CountAsync(j => j.CreatedAt >= startOfDayUtc, cancellationToken);

        return todaysJobs >= _options.MaxTranscriptionsPerDay ? _options.DailyLimitMessage : null;
    }

    public void RecordUpstreamLimitHit(TimeSpan? retryAfter = null)
    {
        var cooldown = retryAfter is { } supplied && supplied > TimeSpan.Zero
            ? supplied
            : _options.UpstreamLimitCooldown;

        lock (_gate)
        {
            var until = timeProvider.GetUtcNow().Add(cooldown);
            // Never shorten an existing block: two jobs failing in the same minute must not let
            // the second one's shorter Retry-After undo the first one's longer cooldown.
            if (_blockedUntilUtc is null || until > _blockedUntilUtc)
            {
                _blockedUntilUtc = until;
            }
        }
    }
}
