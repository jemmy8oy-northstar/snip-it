using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Balenthiran.Snipit.DomainModels.Models;
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
        CutRequest request, ICutService service, IMapper mapper, HttpContext http, CancellationToken ct)
    {
        var words = mapper.Map<List<DomainTranscriptWord>>(request.Words);

        try
        {
            var job = await service.SubmitAsync(request.TranscriptionJobId, words, ct);
            return TypedResults.Ok(ToResponse(job, mapper, http));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<CutJobResponse>, NotFound>> GetJobAsync(
        Guid id, ICutService service, IMapper mapper, HttpContext http, CancellationToken ct)
    {
        var job = await service.GetJobAsync(id, ct);
        return job is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(job, mapper, http));
    }

    internal static async Task<Results<FileStreamHttpResult, NotFound, Conflict<string>>> DownloadAsync(
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

        // Completed in the database is not the same as present on disk — the download link is
        // handed out by this API, so a vanished output has to read as "gone", not "broken".
        var stream = fileStorage.TryOpenRead(job.OutputFilePath);
        if (stream is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(stream, "video/mp4", $"{id}.mp4");
    }

    private static CutJobResponse ToResponse(IDomainCutJob job, IMapper mapper, HttpContext http)
    {
        var response = mapper.Map<CutJobResponse>(job);

        // The browser follows this href directly, so it has to be the path the browser sees, not
        // the one routing sees. In the cluster the app is served under /snipit and UsePathBase
        // strips that prefix before this code runs — without putting it back the link resolves
        // against the host root, which belongs to a different app. PathBase is empty locally, so
        // this is the same string as before there.
        response.DownloadUrl = job.Status == JobStatus.Completed
            ? $"{http.Request.PathBase}/api/cuts/{job.Id}/download"
            : null;

        return response;
    }
}
