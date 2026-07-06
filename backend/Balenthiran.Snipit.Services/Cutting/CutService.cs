using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.EntityModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Snipit.Services.Cutting;

/// <summary>Submit / poll handling for cut jobs. The actual FFmpeg work runs out of band via the job queue.</summary>
public class CutService(
    AppDbContext dbContext,
    IKeepRangeCalculator keepRangeCalculator,
    IBackgroundJobQueue jobQueue,
    IMapper mapper) : ICutService
{
    public async Task<IDomainCutJob> SubmitAsync(Guid transcriptionJobId, List<DomainTranscriptWord> words, CancellationToken cancellationToken = default)
    {
        var transcriptionJob = await dbContext.TranscriptionJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == transcriptionJobId, cancellationToken)
            ?? throw new InvalidOperationException($"Transcription job {transcriptionJobId} not found.");

        var keepRanges = keepRangeCalculator.Calculate(words);
        if (keepRanges.Count == 0)
        {
            throw new InvalidOperationException("No kept words in the cut request — nothing to cut.");
        }

        var entity = new CutJobEntity
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            TranscriptionJobId = transcriptionJobId,
            SourceFilePath = transcriptionJob.SourceFilePath,
            KeepRangesJson = KeepRangeJsonSerializer.Serialize(keepRanges),
        };

        dbContext.CutJobs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var jobId = entity.Id;
        jobQueue.Enqueue((serviceProvider, ct) =>
        {
            var processor = serviceProvider.GetRequiredService<ICutJobProcessor>();
            return processor.ProcessAsync(jobId, ct);
        });

        return mapper.Map<DomainCutJob>(entity);
    }

    public async Task<IDomainCutJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CutJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        return entity is null ? null : mapper.Map<DomainCutJob>(entity);
    }
}
