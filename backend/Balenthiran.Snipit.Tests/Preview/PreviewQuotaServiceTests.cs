using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.EntityModels;
using Balenthiran.Snipit.Services.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Tests.Preview;

public class PreviewQuotaServiceTests
{
    /// <summary>Minimal controllable clock — the alternative is a test that waits 15 real minutes.</summary>
    private sealed class StubClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Noon = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private static (PreviewQuotaService Sut, StubClock Clock, AppDbContext Db) CreateSut(PreviewOptions? options = null)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var clock = new StubClock(Noon);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var sut = new PreviewQuotaService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            clock,
            Options.Create(options ?? new PreviewOptions()));
        return (sut, clock, db);
    }

    private static async Task SeedJobsAsync(AppDbContext db, int count, DateTime createdAt)
    {
        for (var i = 0; i < count; i++)
        {
            db.TranscriptionJobs.Add(new TranscriptionJobEntity
            {
                Id = Guid.NewGuid(),
                Status = JobStatus.Completed,
                CreatedAt = createdAt,
                SourceFilePath = $"uploads/{i}.mp4",
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UnderTheDailyCap_AllowsTheUpload()
    {
        var (sut, _, db) = CreateSut(new PreviewOptions { MaxTranscriptionsPerDay = 3 });
        await SeedJobsAsync(db, 2, Noon.UtcDateTime);

        Assert.Null(await sut.GetBlockReasonAsync());
    }

    [Fact]
    public async Task AtTheDailyCap_BlocksWithTheFriendlyMessage()
    {
        var options = new PreviewOptions { MaxTranscriptionsPerDay = 3 };
        var (sut, _, db) = CreateSut(options);
        await SeedJobsAsync(db, 3, Noon.UtcDateTime);

        var reason = await sut.GetBlockReasonAsync();

        Assert.Equal(options.DailyLimitMessage, reason);
        Assert.Contains("preview", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task YesterdaysJobsDoNotCount()
    {
        var (sut, _, db) = CreateSut(new PreviewOptions { MaxTranscriptionsPerDay = 3 });
        await SeedJobsAsync(db, 50, Noon.UtcDateTime.AddDays(-1));

        Assert.Null(await sut.GetBlockReasonAsync());
    }

    [Fact]
    public async Task TheCapResetsAtUtcMidnightRatherThanOnARollingWindow()
    {
        var (sut, clock, db) = CreateSut(new PreviewOptions { MaxTranscriptionsPerDay = 3 });
        // 23:30 today: three jobs from earlier today still block.
        await SeedJobsAsync(db, 3, Noon.UtcDateTime);
        clock.Now = Noon.AddHours(11.5);
        Assert.NotNull(await sut.GetBlockReasonAsync());

        // 00:30 tomorrow: the same rows are yesterday's, so the allowance is back.
        clock.Now = Noon.AddHours(12.5);
        Assert.Null(await sut.GetBlockReasonAsync());
    }

    [Fact]
    public async Task AnUpstreamLimitBlocksImmediatelyAndExpiresOnItsOwn()
    {
        var options = new PreviewOptions { UpstreamLimitCooldown = TimeSpan.FromMinutes(15) };
        var (sut, clock, _) = CreateSut(options);
        Assert.Null(await sut.GetBlockReasonAsync());

        sut.RecordUpstreamLimitHit();
        Assert.Equal(options.UpstreamLimitMessage, await sut.GetBlockReasonAsync());

        clock.Now = Noon.AddMinutes(14);
        Assert.Equal(options.UpstreamLimitMessage, await sut.GetBlockReasonAsync());

        clock.Now = Noon.AddMinutes(16);
        Assert.Null(await sut.GetBlockReasonAsync());
    }

    [Fact]
    public async Task TheProvidersOwnRetryAfterWins()
    {
        var (sut, clock, _) = CreateSut(new PreviewOptions { UpstreamLimitCooldown = TimeSpan.FromMinutes(15) });

        sut.RecordUpstreamLimitHit(TimeSpan.FromHours(2));

        clock.Now = Noon.AddMinutes(30);
        Assert.NotNull(await sut.GetBlockReasonAsync());
        clock.Now = Noon.AddHours(3);
        Assert.Null(await sut.GetBlockReasonAsync());
    }

    [Fact]
    public async Task ASecondShorterCooldownDoesNotShortenTheFirst()
    {
        // Two jobs failing in the same minute: the second must not undo the first one's longer block.
        var (sut, clock, _) = CreateSut();
        sut.RecordUpstreamLimitHit(TimeSpan.FromHours(2));
        sut.RecordUpstreamLimitHit(TimeSpan.FromMinutes(1));

        clock.Now = Noon.AddMinutes(30);
        Assert.NotNull(await sut.GetBlockReasonAsync());
    }

    [Fact]
    public async Task WithNoDatabaseConfigured_DoesNotBlock()
    {
        // The app boots without a connection string in dev; it cannot store a job either, so
        // there is nothing to protect and the request should fail on its own terms, not this one.
        var clock = new StubClock(Noon);
        var sut = new PreviewQuotaService(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            clock,
            Options.Create(new PreviewOptions()));

        Assert.Null(await sut.GetBlockReasonAsync());
    }
}
