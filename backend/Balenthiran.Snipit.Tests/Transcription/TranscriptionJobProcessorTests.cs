using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.EntityModels;
using Balenthiran.Snipit.Services.Preview;
using Balenthiran.Snipit.Services.Transcription;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Balenthiran.Snipit.Tests.Transcription;

public class TranscriptionJobProcessorTests
{
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IAudioExtractionService> _audioExtraction = new();
    private readonly Mock<IGroqTranscriptionClient> _groqClient = new();

    private static AppDbContext CreateInMemoryDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly Mock<IPreviewQuotaService> _quota = new();
    private readonly PreviewOptions _previewOptions = new();

    private TranscriptionJobProcessor CreateSut(AppDbContext dbContext) => new(
        dbContext, _fileStorage.Object, _audioExtraction.Object, _groqClient.Object,
        _quota.Object, Options.Create(_previewOptions), NullLogger<TranscriptionJobProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_HappyPath_PersistsTranscriptAndMarksCompleted()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var job = new TranscriptionJobEntity { Id = Guid.NewGuid(), Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow, SourceFilePath = "uploads/x.mp4" };
        dbContext.TranscriptionJobs.Add(job);
        await dbContext.SaveChangesAsync();

        _fileStorage.Setup(x => x.GetFullPath("uploads/x.mp4")).Returns("/data/uploads/x.mp4");
        _audioExtraction.Setup(x => x.ExtractAudioAsync("/data/uploads/x.mp4", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string wav, CancellationToken _) =>
            {
                File.WriteAllBytes(wav, [0x52, 0x49, 0x46, 0x46]); // fake wav bytes so File.OpenRead succeeds
                return wav;
            });
        _groqClient.Setup(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroqTranscriptionResult
            {
                DurationSeconds = 5,
                Segments = [new DomainTranscriptSegment { Index = 0, Start = 0, End = 5, Text = "hi" }],
                Words = [new DomainTranscriptWord { Text = "hi", Start = 0, End = 0.5, Kept = true }],
            });

        await CreateSut(dbContext).ProcessAsync(job.Id);

        var updated = await dbContext.TranscriptionJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Completed, updated.Status);
        Assert.NotNull(updated.TranscriptJson);
        var transcript = TranscriptJsonSerializer.Deserialize(updated.TranscriptJson);
        Assert.Equal(5, transcript!.DurationSeconds);
        Assert.Single(transcript.Words);
    }

    [Fact]
    public async Task ProcessAsync_WhenAudioExtractionFails_MarksJobFailedWithoutLeakingTheException()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var job = new TranscriptionJobEntity { Id = Guid.NewGuid(), Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow, SourceFilePath = "uploads/x.mp4" };
        dbContext.TranscriptionJobs.Add(job);
        await dbContext.SaveChangesAsync();

        _fileStorage.Setup(x => x.GetFullPath(It.IsAny<string>())).Returns("/data/uploads/x.mp4");
        _audioExtraction.Setup(x => x.ExtractAudioAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ffmpeg exploded: /data/uploads/x.mp4 host=db.internal"));

        await CreateSut(dbContext).ProcessAsync(job.Id);

        var updated = await dbContext.TranscriptionJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Failed, updated.Status);

        // Error is rendered straight into the editor, which anyone can now reach. Until this
        // change it was ex.Message verbatim, so an ffmpeg or database failure showed a visitor
        // internal paths and hostnames.
        Assert.DoesNotContain("ffmpeg exploded", updated.Error);
        Assert.DoesNotContain("/data/uploads", updated.Error);
        Assert.DoesNotContain("db.internal", updated.Error);
        Assert.Equal("Something went wrong transcribing this video. Try uploading it again.", updated.Error);
    }

    [Fact]
    public async Task ProcessAsync_WhenProviderRateLimits_ShowsThePreviewMessageAndStopsAcceptingUploads()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var job = new TranscriptionJobEntity { Id = Guid.NewGuid(), Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow, SourceFilePath = "uploads/x.mp4" };
        dbContext.TranscriptionJobs.Add(job);
        await dbContext.SaveChangesAsync();

        _fileStorage.Setup(x => x.GetFullPath(It.IsAny<string>())).Returns("/data/uploads/x.mp4");
        _audioExtraction.Setup(x => x.ExtractAudioAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string wav, CancellationToken _) =>
            {
                File.WriteAllBytes(wav, [0x52, 0x49, 0x46, 0x46]);
                return wav;
            });
        _groqClient.Setup(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TranscriptionQuotaExceededException("rate limited (429)", TimeSpan.FromMinutes(3)));

        await CreateSut(dbContext).ProcessAsync(job.Id);

        var updated = await dbContext.TranscriptionJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Failed, updated.Status);
        Assert.Equal(_previewOptions.UpstreamLimitMessage, updated.Error);
        Assert.DoesNotContain("429", updated.Error);

        // The provider's own cooldown is passed through, so we do not keep hammering it.
        _quota.Verify(x => x.RecordUpstreamLimitHit(TimeSpan.FromMinutes(3)), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_UnknownJobId_DoesNotThrow()
    {
        await using var dbContext = CreateInMemoryDbContext();

        await CreateSut(dbContext).ProcessAsync(Guid.NewGuid());
        // no assert needed — just must not throw
    }
}
