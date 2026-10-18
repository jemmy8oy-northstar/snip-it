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

        // The 429 has to be declared by hand: JsonHttpResult<T> only knows its status code at
        // runtime, so it contributes 200 to the OpenAPI document and the generated client would
        // never learn the shape of the body it is expected to read.
        group.MapPost("", SubmitAsync).DisableAntiforgery().WithName("SubmitTranscription")
            .Produces<PreviewLimitReached>(StatusCodes.Status429TooManyRequests);
        group.MapGet("/{id:guid}", GetJobAsync).WithName("GetTranscriptionJob");
        group.MapGet("/{id:guid}/transcript", GetTranscriptAsync).WithName("GetTranscript");

        // There is deliberately no GET /{id}/source (#22). The editor plays the file the visitor
        // picked, straight out of the browser, so serving the video back would mean keeping a copy
        // of it purely to hand it to the one person who already has it.

        return parentGroup;
    }

    private static async Task<Results<Ok<TranscriptionJob>, BadRequest<string>, JsonHttpResult<PreviewLimitReached>>> SubmitAsync(
        IFormFile file,
        ITranscriptionService service,
        IPreviewQuotaService quota,
        IMapper mapper,
        CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded.");
        }

        // Checked before the stream is read, so a blocked upload costs no disk. snip-it is open to
        // anyone by design (#13), which makes this the only thing bounding what a bad day can cost.
        if (await quota.GetBlockReasonAsync(ct) is { } reason)
        {
            return TypedResults.Json(
                new PreviewLimitReached { Message = reason },
                statusCode: StatusCodes.Status429TooManyRequests);
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
