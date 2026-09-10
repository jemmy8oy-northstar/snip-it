using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.Services.Transcription;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Balenthiran.Snipit.Tests.Transcription;

public class TranscriptionServiceTests
{
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IBackgroundJobQueue> _jobQueue = new();
    private readonly IMapper _mapper = TestMapper.Create();

    private static AppDbContext CreateInMemoryDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private TranscriptionService CreateSut(AppDbContext dbContext) => new(dbContext, _fileStorage.Object, _jobQueue.Object, _mapper);

    [Fact]
    public async Task SubmitAsync_SavesFilePersistsPendingJobAndEnqueuesProcessing()
    {
        await using var dbContext = CreateInMemoryDbContext();
        _fileStorage.Setup(x => x.SaveAsync(It.IsAny<Stream>(), "uploads", "clip.mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uploads/abc_clip.mp4");

        using var stream = new MemoryStream([1, 2, 3]);
        var job = await CreateSut(dbContext).SubmitAsync(stream, "clip.mp4");

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal("uploads/abc_clip.mp4", job.SourceFilePath);
        var persisted = await dbContext.TranscriptionJobs.SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Pending, persisted.Status);
        _jobQueue.Verify(x => x.Enqueue(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>()), Times.Once);
    }

    [Fact]
    public async Task GetJobAsync_UnknownId_ReturnsNull()
    {
        await using var dbContext = CreateInMemoryDbContext();

        var job = await CreateSut(dbContext).GetJobAsync(Guid.NewGuid());

        Assert.Null(job);
    }
}
