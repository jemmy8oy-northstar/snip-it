using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Balenthiran.Snipit.WebApi.Routes;

public static class CutRoutes
{
    public static RouteGroupBuilder MapCutRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/cuts");

        group.MapPost("", SubmitAsync).WithName("SubmitCut");
        group.MapGet("/{id:guid}", GetJobAsync).WithName("GetCutJob");
        group.MapGet("/{id:guid}/download", DownloadAsync).WithName("DownloadCut");

        return parentGroup;
    }

    private static async Task<Results<Ok<CutJobResponse>, BadRequest<string>>> SubmitAsync(
        CutRequest request, ICutService service, IMapper mapper, CancellationToken ct)
    {
        var words = mapper.Map<List<DomainTranscriptWord>>(request.Words);

        try
        {
            var job = await service.SubmitAsync(request.TranscriptionJobId, words, ct);
            return TypedResults.Ok(ToResponse(job, mapper));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<CutJobResponse>, NotFound>> GetJobAsync(
        Guid id, ICutService service, IMapper mapper, CancellationToken ct)
    {
        var job = await service.GetJobAsync(id, ct);
        return job is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(job, mapper));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound, Conflict<string>>> DownloadAsync(
        Guid id, ICutService service, IFileStorageService fileStorage, CancellationToken ct)
    {
        var job = await service.GetJobAsync(id, ct);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        if (job.Status != JobStatus.Completed || job.OutputFilePath is null)
        {
            return TypedResults.Conflict($"Cut job is {job.Status} — output not ready yet.");
        }

        var stream = fileStorage.OpenRead(job.OutputFilePath);
        return TypedResults.File(stream, "video/mp4", $"{id}.mp4");
    }

    private static CutJobResponse ToResponse(IDomainCutJob job, IMapper mapper)
    {
        var response = mapper.Map<CutJobResponse>(job);
        response.DownloadUrl = job.Status == JobStatus.Completed ? $"/api/cuts/{job.Id}/download" : null;
        return response;
    }
}
