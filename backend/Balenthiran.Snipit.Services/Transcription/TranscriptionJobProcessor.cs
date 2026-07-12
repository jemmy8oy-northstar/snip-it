using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Database;
using Balenthiran.Snipit.DomainModels.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Transcription job {JobId} failed.", jobId);
            entity.Status = JobStatus.Failed;
            entity.Error = ex.Message;
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
