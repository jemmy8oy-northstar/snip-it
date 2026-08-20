using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.EntityModels;
using Balenthiran.Snipit.Services.Cutting;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Balenthiran.Snipit.Tests.Cutting;

public class CutServiceTests
{
    private readonly Mock<IBackgroundJobQueue> _jobQueue = new();
    private readonly KeepRangeCalculator _keepRangeCalculator = new(); // real: cheap pure logic, no need to mock
    private readonly IMapper _mapper = TestMapper.Create();

    private static AppDbContext CreateInMemoryDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private CutService CreateSut(AppDbContext dbContext) => new(dbContext, _keepRangeCalculator, _jobQueue.Object, _mapper);

    private static DomainTranscriptWord Word(double s, double e, bool kept) => new() { Text = "w", Start = s, End = e, Kept = kept };

    [Fact]
    public async Task SubmitAsync_CreatesPendingJobWithComputedKeepRangesAndEnqueuesProcessing()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var transcriptionJob = await SeedTranscriptionJobAsync(dbContext);

        var words = new List<DomainTranscriptWord> { Word(0, 1, true), Word(1, 2, false), Word(2, 3, true) };

        var job = await CreateSut(dbContext).SubmitAsync(transcriptionJob.Id, "cut-sources/fresh.mp4", words);

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal("cut-sources/fresh.mp4", job.SourceFilePath);
        Assert.Equal(2, job.KeepRanges.Count);
        _jobQueue.Verify(x => x.Enqueue(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>()), Times.Once);
    }

    /// <summary>
    /// The cut runs against the file that came with the request, never the transcription job's own
    /// path (#22) — that upload was deleted the moment transcription finished, so reading it back
    /// would resolve a key whose bytes are guaranteed to be gone. Both are set here, and to
    /// different values, so a regression to the old source cannot pass by coincidence.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_UsesTheUploadedSource_NotTheTranscriptionJobsDeletedOne()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var transcriptionJob = await SeedTranscriptionJobAsync(dbContext, "uploads/already-deleted.mp4");

        var job = await CreateSut(dbContext)
            .SubmitAsync(transcriptionJob.Id, "cut-sources/re-sent.mp4", [Word(0, 1, true)]);

        Assert.Equal("cut-sources/re-sent.mp4", job.SourceFilePath);
        Assert.NotEqual("uploads/already-deleted.mp4", job.SourceFilePath);
    }

    [Fact]
    public async Task SubmitAsync_UnknownTranscriptionJob_Throws()
    {
        await using var dbContext = CreateInMemoryDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(dbContext).SubmitAsync(Guid.NewGuid(), "cut-sources/fresh.mp4", [Word(0, 1, true)]));
    }

    [Fact]
    public async Task SubmitAsync_NoKeptWords_ThrowsWithoutPersisting()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var transcriptionJob = await SeedTranscriptionJobAsync(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(dbContext).SubmitAsync(transcriptionJob.Id, "cut-sources/fresh.mp4", [Word(0, 1, false)]));

        Assert.Empty(dbContext.CutJobs);
    }

    private static async Task<TranscriptionJobEntity> SeedTranscriptionJobAsync(
        AppDbContext dbContext, string sourceFilePath = "uploads/x.mp4")
    {
        var transcriptionJob = new TranscriptionJobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            SourceFilePath = sourceFilePath,
        };
        dbContext.TranscriptionJobs.Add(transcriptionJob);
        await dbContext.SaveChangesAsync();
        return transcriptionJob;
    }

    [Fact]
    public async Task GetJobAsync_UnknownId_ReturnsNull()
    {
        await using var dbContext = CreateInMemoryDbContext();

        var job = await CreateSut(dbContext).GetJobAsync(Guid.NewGuid());

        Assert.Null(job);
    }
}
