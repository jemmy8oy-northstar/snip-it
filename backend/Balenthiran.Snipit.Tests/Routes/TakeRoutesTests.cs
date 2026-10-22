using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DataModels.Models;
using Balenthiran.Snipit.Services.Infrastructure;
using Balenthiran.Snipit.WebApi.Routes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Tests.Routes;

/// <summary>
/// BEH-EDITION-3 at the HTTP edge, over a real store on a temp directory: what Kit's agent sees is
/// what these handlers answer. Off (no root) must read as 404 on every route, never as an empty list.
/// </summary>
public class TakeRoutesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"snipit-take-routes-{Guid.NewGuid():N}");
    private readonly Guid _production = Guid.NewGuid();

    private ITakeStore Store(string? root = null, long max = 1024) =>
        new LocalDiskTakeStore(Options.Create(new TakeStoreOptions { RootPath = root ?? _root, MaxTotalBytes = max }));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static FormFile Upload(string name, params byte[] bytes) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    [Fact]
    public async Task Save_ThenListThenFetch_RoundTripsTheTake()
    {
        var store = Store();

        var saved = await TakeRoutes.SaveAsync(_production, Upload("take-1.mov", 1, 2, 3), store, CancellationToken.None);
        var listed = TakeRoutes.List(_production, store);
        var fetched = TakeRoutes.Download(_production, "take-1.mov", store);

        Assert.Equal("take-1.mov", Assert.IsType<Ok<SavedTake>>(saved.Result).Value!.Name);
        Assert.Equal(3, Assert.Single(Assert.IsType<Ok<List<SavedTake>>>(listed.Result).Value!).Bytes);
        var file = Assert.IsType<FileStreamHttpResult>(fetched.Result);
        Assert.Equal("video/quicktime", file.ContentType);
        file.FileStream.Dispose();
    }

    [Fact]
    public async Task WhenSavingIsOff_EveryRouteIs404()
    {
        var store = Store(root: "");

        Assert.IsType<NotFound>((await TakeRoutes.SaveAsync(_production, Upload("take-1.mov", 1), store, CancellationToken.None)).Result);
        Assert.IsType<NotFound>(TakeRoutes.List(_production, store).Result);
        Assert.IsType<NotFound>(TakeRoutes.Download(_production, "take-1.mov", store).Result);
    }

    [Theory]
    [InlineData("../x.mov")]
    [InlineData("..%2Fx.mov")]
    [InlineData(".hidden")]
    public async Task APathTraversalName_IsRefusedOnSaveAndNotFoundOnFetch(string name)
    {
        var store = Store();

        var saved = await TakeRoutes.SaveAsync(_production, Upload(name, 1), store, CancellationToken.None);
        var fetched = TakeRoutes.Download(_production, name, store);

        Assert.IsType<BadRequest<string>>(saved.Result);
        Assert.IsType<NotFound>(fetched.Result);
    }

    [Fact]
    public async Task AnEmptyUpload_IsABadRequest()
    {
        var saved = await TakeRoutes.SaveAsync(_production, Upload("take-1.mov"), Store(), CancellationToken.None);

        Assert.IsType<BadRequest<string>>(saved.Result);
    }

    [Fact]
    public async Task APastTheCapUpload_Is507()
    {
        var saved = await TakeRoutes.SaveAsync(_production, Upload("take-1.mov", 1, 2, 3), Store(max: 2), CancellationToken.None);

        var status = Assert.IsType<StatusCodeHttpResult>(saved.Result);
        Assert.Equal(StatusCodes.Status507InsufficientStorage, status.StatusCode);
    }

    [Fact]
    public void AMissingTake_Is404()
    {
        Assert.IsType<NotFound>(TakeRoutes.Download(_production, "nope.mov", Store()).Result);
    }
}
