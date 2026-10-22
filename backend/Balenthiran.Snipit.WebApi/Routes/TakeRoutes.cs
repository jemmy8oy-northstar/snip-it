using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Balenthiran.Snipit.WebApi.Routes;

/// <summary>
/// Saved takes (BEH-EDITION-3). A take only arrives here when the creator pressed Save on it; Kit's
/// agent reads it back through these routes rather than by mounting the volume. Like the rest of
/// snip-it the routes carry no sign-in of their own (#13); the production id (a random Guid) is the
/// address, and <c>Takes:MaxTotalBytes</c> bounds what an open write route can cost.
/// </summary>
public static class TakeRoutes
{
    public static RouteGroupBuilder MapTakeRoutes(this RouteGroupBuilder parentGroup)
    {
        var group = parentGroup.MapGroup("/productions/{productionId:guid}/takes");

        group.MapPost("", SaveAsync).DisableAntiforgery().WithName("SaveTake");
        group.MapGet("", List).WithName("ListTakes");
        group.MapGet("/{name}", Download).WithName("GetTake");

        return parentGroup;
    }

    /// <summary>Saves the uploaded file under its own file name; saving the same take again replaces it.</summary>
    internal static async Task<Results<Ok<SavedTake>, BadRequest<string>, NotFound, StatusCodeHttpResult>> SaveAsync(
        Guid productionId, IFormFile file, ITakeStore store, CancellationToken ct)
    {
        if (!store.Enabled)
        {
            return TypedResults.NotFound();
        }

        if (file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded.");
        }

        if (!store.IsValidName(file.FileName))
        {
            return TypedResults.BadRequest("The take's file name may only use letters, digits, dot, dash and underscore.");
        }

        await using var stream = file.OpenReadStream();
        var saved = await store.SaveAsync(productionId, file.FileName, stream, ct);
        return saved is null
            ? TypedResults.StatusCode(StatusCodes.Status507InsufficientStorage)
            : TypedResults.Ok(new SavedTake(saved.Name, saved.Bytes, saved.SavedAt));
    }

    internal static Results<Ok<List<SavedTake>>, NotFound> List(Guid productionId, ITakeStore store) =>
        store.Enabled
            ? TypedResults.Ok(store.List(productionId).Select(t => new SavedTake(t.Name, t.Bytes, t.SavedAt)).ToList())
            : TypedResults.NotFound();

    internal static Results<FileStreamHttpResult, NotFound> Download(Guid productionId, string name, ITakeStore store)
    {
        // An invalid name is not an error worth distinguishing: nothing is stored under it.
        if (!store.Enabled || !store.IsValidName(name))
        {
            return TypedResults.NotFound();
        }

        var stream = store.TryOpen(productionId, name);
        if (stream is null)
        {
            return TypedResults.NotFound();
        }

        var contentType = name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? "video/mp4" : "video/quicktime";
        return TypedResults.File(stream, contentType, name, enableRangeProcessing: true);
    }
}
