using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balenthiran.Snipit.Services.Cutting;

/// <summary>
/// Runs one cut job end to end: resolve the source file, drive FFmpeg to trim + concat the
/// kept ranges, persist the output path. Invoked by the background job queue after
/// <see cref="CutService.SubmitAsync"/>.
/// </summary>
public class CutJobProcessor(
    AppDbContext dbContext,
    IFileStorageService fileStorage,
    IVideoCutService videoCutService,
    ILogger<CutJobProcessor> logger) : ICutJobProcessor
{
    private const string OutputFolder = "exports";

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CutJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Cut job {JobId} not found — skipping.", jobId);
            return;
        }

        entity.Status = JobStatus.Processing;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var sourceFullPath = fileStorage.GetFullPath(entity.SourceFilePath);
            var outputStorageKey = $"{OutputFolder}/{jobId:N}.mp4";
            var outputFullPath = fileStorage.GetFullPath(outputStorageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath)!);

            var keepRanges = KeepRangeJsonSerializer.Deserialize(entity.KeepRangesJson);
            await videoCutService.CutAsync(sourceFullPath, keepRanges, outputFullPath, cancellationToken);

            entity.OutputFilePath = outputStorageKey;
            entity.Status = JobStatus.Completed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cut job {JobId} failed.", jobId);
            entity.Status = JobStatus.Failed;
            entity.Error = ex.Message;
        }
        finally
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
