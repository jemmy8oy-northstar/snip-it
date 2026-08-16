using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.Services.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>
/// Runs one transcription job end to end: extract audio with FFmpeg, transcribe with Groq,
/// persist the transcript. Invoked by the background job queue after <see cref="TranscriptionService.SubmitAsync"/>.
/// </summary>
public class TranscriptionJobProcessor(
    AppDbContext dbContext,
    IFileStorageService fileStorage,
    IAudioExtractionService audioExtraction,
    IGroqTranscriptionClient groqClient,
    IPreviewQuotaService quota,
    IOptions<PreviewOptions> previewOptions,
    ILogger<TranscriptionJobProcessor> logger) : ITranscriptionJobProcessor
{
    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.TranscriptionJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Transcription job {JobId} not found — skipping.", jobId);
            return;
        }

        entity.Status = JobStatus.Processing;
        await dbContext.SaveChangesAsync(cancellationToken);

        var wavPath = Path.Combine(Path.GetTempPath(), $"snipit-{jobId:N}.wav");
        try
        {
            var sourceFullPath = fileStorage.GetFullPath(entity.SourceFilePath);
            await audioExtraction.ExtractAudioAsync(sourceFullPath, wavPath, cancellationToken);

            await using var audioStream = File.OpenRead(wavPath);
            var result = await groqClient.TranscribeAsync(audioStream, Path.GetFileName(wavPath), cancellationToken);

            var transcript = new DomainTranscript
            {
                DurationSeconds = result.DurationSeconds,
                Segments = result.Segments
                    .Select(s => new DomainTranscriptSegment { Index = s.Index, Start = s.Start, End = s.End, Text = s.Text })
                    .ToList(),
                Words = result.Words
                    .Select(w => new DomainTranscriptWord { Text = w.Text, Start = w.Start, End = w.End, Kept = w.Kept })
                    .ToList(),
            };

            entity.TranscriptJson = TranscriptJsonSerializer.Serialize(transcript);
            entity.Status = JobStatus.Completed;
        }
        catch (TranscriptionQuotaExceededException ex)
        {
            // Not a fault — snip-it is open to anyone and has simply run out of transcription
            // capacity for now. Stop accepting uploads we already know we cannot process, and
            // tell the visitor in words rather than handing them the provider's error object.
            logger.LogWarning(ex, "Transcription job {JobId} hit the provider's rate limit.", jobId);
            quota.RecordUpstreamLimitHit(ex.RetryAfter);
            entity.Status = JobStatus.Failed;
            entity.Error = previewOptions.Value.UpstreamLimitMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transcription job {JobId} failed.", jobId);
            entity.Status = JobStatus.Failed;
            // ex.Message is rendered straight into the editor, which anyone can reach — so it says
            // what went wrong without quoting file paths, connection strings or provider payloads.
            entity.Error = "Something went wrong transcribing this video. Try uploading it again.";
        }
        finally
        {
            if (File.Exists(wavPath))
            {
                File.Delete(wavPath);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
