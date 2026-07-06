using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Balenthiran.Snipit.WebApi.Routes;

public static class TranscriptionRoutes
{
    public static RouteGroupBuilder MapTranscriptionRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/transcriptions");

        group.MapPost("", SubmitAsync).DisableAntiforgery().WithName("SubmitTranscription");
        group.MapGet("/{id:guid}", GetJobAsync).WithName("GetTranscriptionJob");
        group.MapGet("/{id:guid}/transcript", GetTranscriptAsync).WithName("GetTranscript");

        return parentGroup;
    }

    private static async Task<Results<Ok<TranscriptionJob>, BadRequest<string>>> SubmitAsync(
        IFormFile file, ITranscriptionService service, IMapper mapper, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded.");
        }

        await using var stream = file.OpenReadStream();
        var job = await service.SubmitAsync(stream, file.FileName, ct);

        return TypedResults.Ok(mapper.Map<TranscriptionJob>(job));
    }

    private static async Task<Results<Ok<TranscriptionJob>, NotFound>> GetJobAsync(
        Guid id, ITranscriptionService service, IMapper mapper, CancellationToken ct)
    {
        var job = await service.GetJobAsync(id, ct);
        return job is null ? TypedResults.NotFound() : TypedResults.Ok(mapper.Map<TranscriptionJob>(job));
    }

    private static async Task<Results<Ok<Transcript>, NotFound, Conflict<string>>> GetTranscriptAsync(
        Guid id, ITranscriptionService service, IMapper mapper, CancellationToken ct)
    {
        var job = await service.GetJobAsync(id, ct);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        if (job.Status != JobStatus.Completed || job.Transcript is null)
        {
            return TypedResults.Conflict($"Transcription job is {job.Status} — transcript not ready yet.");
        }

        var dto = new Transcript
        {
            TranscriptionJobId = job.Id,
            DurationSeconds = job.Transcript.DurationSeconds,
            Segments = mapper.Map<List<TranscriptSegment>>(job.Transcript.Segments),
            Words = mapper.Map<List<TranscriptWord>>(job.Transcript.Words),
        };

        return TypedResults.Ok(dto);
    }
}
