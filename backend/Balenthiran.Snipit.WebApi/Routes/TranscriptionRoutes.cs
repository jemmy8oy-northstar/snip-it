using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;

namespace Balenthiran.Snipit.WebApi.Routes;

public static class TranscriptionRoutes
{
    public static RouteGroupBuilder MapTranscriptionRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/transcriptions");

        group.MapPost("", async (IFormFile file, ITranscriptionService service, IMapper mapper, CancellationToken ct) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            await using var stream = file.OpenReadStream();
            var job = await service.SubmitAsync(stream, file.FileName, ct);

            return Results.Ok(mapper.Map<TranscriptionJob>(job));
        })
        .DisableAntiforgery()
        .WithName("SubmitTranscription");

        group.MapGet("/{id:guid}", async (Guid id, ITranscriptionService service, IMapper mapper, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(id, ct);
            return job is null ? Results.NotFound() : Results.Ok(mapper.Map<TranscriptionJob>(job));
        })
        .WithName("GetTranscriptionJob");

        group.MapGet("/{id:guid}/transcript", async (Guid id, ITranscriptionService service, IMapper mapper, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(id, ct);
            if (job is null)
            {
                return Results.NotFound();
            }

            if (job.Status != JobStatus.Completed || job.Transcript is null)
            {
                return Results.Conflict($"Transcription job is {job.Status} — transcript not ready yet.");
            }

            var dto = new Transcript
            {
                TranscriptionJobId = job.Id,
                DurationSeconds = job.Transcript.DurationSeconds,
                Segments = mapper.Map<List<TranscriptSegment>>(job.Transcript.Segments),
                Words = mapper.Map<List<TranscriptWord>>(job.Transcript.Words),
            };

            return Results.Ok(dto);
        })
        .WithName("GetTranscript");

        return parentGroup;
    }
}
