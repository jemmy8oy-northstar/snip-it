using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.EntityModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>Submit / poll handling for transcription jobs. The actual pipeline runs out of band via the job queue.</summary>
public class TranscriptionService(
    AppDbContext dbContext,
    IFileStorageService fileStorage,
    IBackgroundJobQueue jobQueue) : ITranscriptionService
{
    private const string StorageFolder = "uploads";

    public async Task<IDomainTranscriptionJob> SubmitAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var storageKey = await fileStorage.SaveAsync(fileStream, StorageFolder, fileName, cancellationToken);

        var entity = new TranscriptionJobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SourceFilePath = storageKey,
        };

        dbContext.TranscriptionJobs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var jobId = entity.Id;
        jobQueue.Enqueue((serviceProvider, ct) =>
        {
            var processor = serviceProvider.GetRequiredService<ITranscriptionJobProcessor>();
            return processor.ProcessAsync(jobId, ct);
        });

        return ToDomain(entity);
    }

    public async Task<IDomainTranscriptionJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.TranscriptionJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    private static DomainTranscriptionJob ToDomain(TranscriptionJobEntity entity) => new()
    {
        Id = entity.Id,
        Status = entity.Status,
        Error = entity.Error,
        CreatedAt = entity.CreatedAt,
        SourceFilePath = entity.SourceFilePath,
        Transcript = TranscriptJsonSerializer.Deserialize(entity.TranscriptJson),
    };
}
