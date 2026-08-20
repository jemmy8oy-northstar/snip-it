using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
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
                .Setup(x => x.CutAsync(sourcePath, It.Is<IReadOnlyList<IDomainKeepRange>>(r => r.Count == 1), outputPath, It.IsAny<CancellationToken>()))
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
    public async Task ProcessAsync_WhenFfmpegFails_MarksJobFailed()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var job = await FailJobWith(dbContext, new InvalidOperationException("ffmpeg exploded"));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.False(string.IsNullOrWhiteSpace(job.Error));
    }

    /// <summary>
    /// The visitor-facing half of the same rule <see cref="Balenthiran.Snipit.Services.Transcription.TranscriptionJobProcessor"/>
    /// already documents: <c>CutJob.Error</c> is rendered straight into the editor, which anyone can
    /// reach, so it must not carry the exception detail. FFmpeg's stderr is quoted verbatim into the
    /// exception by <see cref="Balenthiran.Snipit.Services.Cutting.VideoCutService"/> — deliberately,
    /// because the log needs it — and that stderr always names the absolute source and output paths.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenFfmpegFails_DoesNotLeakServerPathsToTheVisitor()
    {
        const string RealFfmpegStderr = """
            ffmpeg version 6.1.1-3ubuntu5 Copyright (c) 2000-2023 the FFmpeg developers
              configuration: --prefix=/usr --extra-version=3ubuntu5 --libdir=/usr/lib/x86_64-linux-gnu
            [in#0 @ 0x5f2c1a] Error opening input: No such file or directory
            Error opening input file /data/storage/uploads/9f3c2b1d_holiday.mp4.
            """;

        await using var dbContext = CreateInMemoryDbContext();
        var job = await FailJobWith(
            dbContext,
            new InvalidOperationException($"FFmpeg cut failed (exit code 1): {RealFfmpegStderr}"));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.DoesNotContain("/data/storage", job.Error);
        Assert.DoesNotContain("ffmpeg", job.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exit code", job.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The other exception that reaches this catch carries a client-supplied storage key rather than
    /// FFmpeg output — a traversal attempt refused by <c>LocalDiskFileStorageService.GetFullPath</c>.
    /// Echoing it back would confirm to whoever sent it exactly what the server did with their input.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenTheStorageKeyIsRefused_DoesNotEchoItBack()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var job = await FailJobWith(
            dbContext,
            new InvalidOperationException("Storage key '../../etc/passwd' resolves outside the storage root."));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.DoesNotContain("etc/passwd", job.Error);
        Assert.DoesNotContain("storage root", job.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs one cut job whose FFmpeg step throws <paramref name="failure"/>, and returns the persisted
    /// row. Shared so every leak assertion is made against the same real processing path — a bespoke
    /// per-test setup is how one of them ends up asserting against a job that never ran.
    /// </summary>
    private async Task<CutJobEntity> FailJobWith(AppDbContext dbContext, Exception failure)
    {
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

        var root = Directory.CreateTempSubdirectory("snipit-cut-test-").FullName;
        try
        {
            _fileStorage.Setup(x => x.GetFullPath(It.IsAny<string>())).Returns<string>(s => Path.Combine(root, s));
            _videoCutService
                .Setup(x => x.CutAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<IDomainKeepRange>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            await CreateSut(dbContext).ProcessAsync(job.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return await dbContext.CutJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
    }
}
