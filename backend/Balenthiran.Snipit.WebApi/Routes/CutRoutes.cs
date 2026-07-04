using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;

namespace Balenthiran.Snipit.WebApi.Routes;

public static class CutRoutes
{
    public static RouteGroupBuilder MapCutRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/cuts");

        group.MapPost("", async (CutRequest request, ICutService service, IMapper mapper, CancellationToken ct) =>
        {
            var words = mapper.Map<List<DomainTranscriptWord>>(request.Words);

            try
            {
                var job = await service.SubmitAsync(request.TranscriptionJobId, words, ct);
                return Results.Ok(ToResponse(job, mapper));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("SubmitCut");

        group.MapGet("/{id:guid}", async (Guid id, ICutService service, IMapper mapper, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(id, ct);
            return job is null ? Results.NotFound() : Results.Ok(ToResponse(job, mapper));
        })
        .WithName("GetCutJob");

        group.MapGet("/{id:guid}/download", async (Guid id, ICutService service, IFileStorageService fileStorage, CancellationToken ct) =>
        {
            var job = await service.GetJobAsync(id, ct);
            if (job is null)
            {
                return Results.NotFound();
            }

            if (job.Status != JobStatus.Completed || job.OutputFilePath is null)
            {
                return Results.Conflict($"Cut job is {job.Status} — output not ready yet.");
            }

            var stream = fileStorage.OpenRead(job.OutputFilePath);
            return Results.File(stream, "video/mp4", $"{id}.mp4");
        })
        .WithName("DownloadCut");

        return parentGroup;
    }

    private static CutJobResponse ToResponse(IDomainCutJob job, IMapper mapper)
    {
        var response = mapper.Map<CutJobResponse>(job);
        response.DownloadUrl = job.Status == JobStatus.Completed ? $"/api/cuts/{job.Id}/download" : null;
        return response;
    }
}
