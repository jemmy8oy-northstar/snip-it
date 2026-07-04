using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.EntityModels;
using Balenthiran.Snipit.Services.Cutting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Balenthiran.Snipit.Tests.Cutting;

public class CutJobProcessorTests
{
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IVideoCutService> _videoCutService = new();

    private static AppDbContext CreateInMemoryDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private CutJobProcessor CreateSut(AppDbContext dbContext) =>
        new(dbContext, _fileStorage.Object, _videoCutService.Object, NullLogger<CutJobProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_HappyPath_CutsVideoAndMarksCompleted()
    {
        var root = Directory.CreateTempSubdirectory("snipit-cut-test-").FullName;
        try
        {
            await using var dbContext = CreateInMemoryDbContext();
            var ranges = new List<DomainKeepRange> { new(0, 3.2) };
            var job = new CutJobEntity
            {
                Id = Guid.NewGuid(),
                Status = JobStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TranscriptionJobId = Guid.NewGuid(),
                SourceFilePath = "uploads/x.mp4",
                KeepRangesJson = KeepRangeJsonSerializer.Serialize(ranges),
            };
            dbContext.CutJobs.Add(job);
            await dbContext.SaveChangesAsync();

            var sourcePath = Path.Combine(root, "uploads", "x.mp4");
            var outputPath = Path.Combine(root, "exports", "out.mp4");
            _fileStorage.Setup(x => x.GetFullPath("uploads/x.mp4")).Returns(sourcePath);
            _fileStorage.Setup(x => x.GetFullPath(It.Is<string>(s => s.StartsWith("exports/")))).Returns(outputPath);
            _videoCutService
                .Setup(x => x.CutAsync(sourcePath, It.Is<List<DomainKeepRange>>(r => r.Count == 1), outputPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(outputPath);

            await CreateSut(dbContext).ProcessAsync(job.Id);

            var updated = await dbContext.CutJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
            Assert.Equal(JobStatus.Completed, updated.Status);
            Assert.StartsWith("exports/", updated.OutputFilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenFfmpegFails_MarksJobFailedWithErrorMessage()
    {
        var root = Directory.CreateTempSubdirectory("snipit-cut-test-").FullName;
        try
        {
            await using var dbContext = CreateInMemoryDbContext();
            var job = new CutJobEntity
            {
                Id = Guid.NewGuid(),
                Status = JobStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TranscriptionJobId = Guid.NewGuid(),
                SourceFilePath = "uploads/x.mp4",
                KeepRangesJson = KeepRangeJsonSerializer.Serialize([new DomainKeepRange(0, 1)]),
            };
            dbContext.CutJobs.Add(job);
            await dbContext.SaveChangesAsync();

            _fileStorage.Setup(x => x.GetFullPath(It.IsAny<string>())).Returns<string>(s => Path.Combine(root, s));
            _videoCutService
                .Setup(x => x.CutAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DomainKeepRange>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ffmpeg exploded"));

            await CreateSut(dbContext).ProcessAsync(job.Id);

            var updated = await dbContext.CutJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
            Assert.Equal(JobStatus.Failed, updated.Status);
            Assert.Equal("ffmpeg exploded", updated.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
