using AutoMapper;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Balenthiran.Snipit.DomainModels.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Balenthiran.Snipit.WebApi.Routes;

public static class CutRoutes
{
    // The editor serialises this payload with JSON.stringify, i.e. camelCase, which is also what
    // the rest of the API speaks. Minimal APIs apply that convention to a JSON *body* for free;
    // a string pulled out of a form field is deserialised by hand, so it has to be said here.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapCutRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/cuts");

        group.MapPost("", SubmitAsync).DisableAntiforgery().WithName("SubmitCut");
        group.MapGet("/{id:guid}", GetJobAsync).WithName("GetCutJob");
        group.MapGet("/{id:guid}/download", DownloadAsync).WithName("DownloadCut");

        return parentGroup;
    }

    private const string SourceFolder = "cut-sources";

    /// <summary>
    /// Submits a cut. Multipart rather than JSON, because the video comes with it: snip-it keeps
    /// no copy of the source between requests (#22), so the browser — which still holds the file
    /// the visitor picked — sends it again alongside the word list.
    /// </summary>
    /// <remarks>
    /// The word list travels as a JSON string in the <c>request</c> form field rather than as form
    /// fields of its own. A cut request is a few hundred words of nested objects, and form encoding
    /// that means <c>words[0].start</c>-style keys on both sides; one JSON field keeps the payload
    /// the editor already builds intact and unambiguous.
    /// </remarks>
    private static async Task<Results<Ok<CutJobResponse>, BadRequest<string>>> SubmitAsync(
        IFormFile file,
        [FromForm] string request,
        ICutService service,
        IFileStorageService fileStorage,
        IMapper mapper,
        HttpContext http,
        CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded.");
        }

        CutRequest? cutRequest;
        try
        {
            cutRequest = JsonSerializer.Deserialize<CutRequest>(request, JsonOptions);
        }
        catch (JsonException)
        {
            // The message is not echoed back: it quotes the offending payload, which is client
            // input rendered into the editor. Same rule the job processors follow.
            return TypedResults.BadRequest("The cut request was not valid JSON.");
        }

        if (cutRequest is null)
        {
            return TypedResults.BadRequest("The cut request was empty.");
        }

        var words = mapper.Map<List<DomainTranscriptWord>>(cutRequest.Words);

        await using var stream = file.OpenReadStream();
        var sourceStorageKey = await fileStorage.SaveAsync(stream, SourceFolder, file.FileName, ct);

        try
        {
            var job = await service.SubmitAsync(cutRequest.TranscriptionJobId, sourceStorageKey, words, ct);
            return TypedResults.Ok(ToResponse(job, mapper, http));
        }
        catch (InvalidOperationException ex)
        {
            // A refused submit has no job, so nothing will ever run the finally block that would
            // otherwise clean this up — and "nothing kept in the cut" is a request a visitor can
            // make by accident, so it must not be the way to leave whole videos on the disk.
            fileStorage.Delete(sourceStorageKey);
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
        // Under #22 that is now the *normal* end state rather than an edge case: the export is
        // deleted as it is streamed, so a second request for the same cut is a legitimate 404.
        var stream = fileStorage.TryOpenReadOnce(job.OutputFilePath);
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
