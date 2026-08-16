using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.WebApi.Routes;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace Balenthiran.Snipit.Tests.Routes;

/// <summary>
/// The two endpoints that stream bytes read a path out of a Postgres row and hand it to file
/// storage. The row and the file have independent lifetimes — the row survives anything that
/// removes the file (a retention sweep, a lost volume, a half-finished write) — so "the job exists
/// but its media does not" is a reachable state, and it is a 404, not a server error.
///
/// Being honest about what this is worth: in the editor a visitor cannot tell the two apart — the
/// &lt;video&gt; element has no error handler and looks equally broken either way. What changes is
/// on the server. An unhandled exception is a crash: it logs at error level and, the moment
/// anything alerts on those, a routine "that recording expired" pages someone at 3am. It is also
/// what these two routes already promise — both declare NotFound in their return type, and the
/// committed openapi.json documents a 404 that the code could not actually produce.
/// </summary>
public class MissingMediaTests
{
    private readonly Mock<IFileStorageService> _fileStorage = new();

    public MissingMediaTests()
    {
        // What storage reports for a key whose file is no longer on disk.
        _fileStorage.Setup(s => s.TryOpenRead(It.IsAny<string>())).Returns((Stream?)null);
    }

    [Fact]
    public async Task GetSource_WhenTheUploadedFileIsGone_Returns404NotAServerError()
    {
        var job = new Mock<IDomainTranscriptionJob>();
        job.SetupGet(j => j.SourceFilePath).Returns("uploads/deadbeef_video.mp4");
        var service = new Mock<ITranscriptionService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(job.Object);

        var result = await TranscriptionRoutes.GetSourceAsync(
            Guid.NewGuid(), service.Object, _fileStorage.Object, StubMediaTypeResolver(), CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task DownloadCut_WhenTheOutputFileIsGone_Returns404NotAServerError()
    {
        var service = new Mock<ICutService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedCutJob("cuts/deadbeef_out.mp4"));

        var result = await CutRoutes.DownloadAsync(
            Guid.NewGuid(), service.Object, _fileStorage.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    // The controls. Without these, "always return NotFound" would pass the two tests above, and
    // the fix would have removed the feature rather than the crash.

    [Fact]
    public async Task GetSource_WhenTheFileIsThere_StillStreamsIt()
    {
        _fileStorage.Setup(s => s.TryOpenRead(It.IsAny<string>())).Returns(new MemoryStream([1, 2, 3]));
        var job = new Mock<IDomainTranscriptionJob>();
        job.SetupGet(j => j.SourceFilePath).Returns("uploads/deadbeef_video.mp4");
        var service = new Mock<ITranscriptionService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(job.Object);

        var result = await TranscriptionRoutes.GetSourceAsync(
            Guid.NewGuid(), service.Object, _fileStorage.Object, StubMediaTypeResolver(), CancellationToken.None);

        var file = Assert.IsType<FileStreamHttpResult>(result.Result);
        Assert.True(file.EnableRangeProcessing, "the editor scrubs the source — it must stay a ranged response");
    }

    [Fact]
    public async Task DownloadCut_WhenTheFileIsThere_StillStreamsIt()
    {
        _fileStorage.Setup(s => s.TryOpenRead(It.IsAny<string>())).Returns(new MemoryStream([1, 2, 3]));
        var service = new Mock<ICutService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedCutJob("cuts/deadbeef_out.mp4"));

        var result = await CutRoutes.DownloadAsync(
            Guid.NewGuid(), service.Object, _fileStorage.Object, CancellationToken.None);

        Assert.IsType<FileStreamHttpResult>(result.Result);
    }

    private static IDomainCutJob CompletedCutJob(string outputFilePath)
    {
        var job = new Mock<IDomainCutJob>();
        job.SetupGet(j => j.Status).Returns(JobStatus.Completed);
        job.SetupGet(j => j.OutputFilePath).Returns(outputFilePath);
        return job.Object;
    }

    private static IUploadMediaTypeResolver StubMediaTypeResolver()
    {
        var resolver = new Mock<IUploadMediaTypeResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns("video/mp4");
        return resolver.Object;
    }
}
