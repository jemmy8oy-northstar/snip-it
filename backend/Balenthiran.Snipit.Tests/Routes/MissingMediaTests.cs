using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.WebApi.Routes;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace Balenthiran.Snipit.Tests.Routes;

/// <summary>
/// The one endpoint that streams bytes reads a path out of a Postgres row and hands it to file
/// storage. The row and the file have independent lifetimes, so "the job exists but its media does
/// not" is a reachable state, and it is a 404, not a server error.
///
/// Since #22 it is not merely reachable but <em>ordinary</em>: there is no volume, the export is
/// deleted as it is streamed, and a restart empties the scratch directory under a database that
/// survived. So this is the normal end state of every completed cut, not an edge case.
///
/// What it is worth: an unhandled exception is a crash. It logs at error level and, the moment
/// anything alerts on those, a routine "that export is gone" pages someone at 3am. It is also what
/// this route already promises — it declares NotFound in its return type, and the committed
/// openapi.json documents a 404 the code could not otherwise produce.
/// </summary>
public class MissingMediaTests
{
    private readonly Mock<IFileStorageService> _fileStorage = new();

    public MissingMediaTests()
    {
        // What storage reports for a key whose file is no longer on disk.
        _fileStorage.Setup(s => s.TryOpenReadOnce(It.IsAny<string>())).Returns((Stream?)null);
    }

    [Fact]
    public async Task DownloadCut_WhenTheOutputFileIsGone_Returns404NotAServerError()
    {
        var service = new Mock<ICutService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedCutJob("exports/deadbeef_out.mp4"));

        var result = await CutRoutes.DownloadAsync(
            Guid.NewGuid(), service.Object, _fileStorage.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    // The control. Without it, "always return NotFound" would pass the test above, and the fix
    // would have removed the feature rather than the crash.

    [Fact]
    public async Task DownloadCut_WhenTheFileIsThere_StillStreamsIt()
    {
        _fileStorage.Setup(s => s.TryOpenReadOnce(It.IsAny<string>())).Returns(new MemoryStream([1, 2, 3]));
        var service = new Mock<ICutService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedCutJob("exports/deadbeef_out.mp4"));

        var result = await CutRoutes.DownloadAsync(
            Guid.NewGuid(), service.Object, _fileStorage.Object, CancellationToken.None);

        Assert.IsType<FileStreamHttpResult>(result.Result);
    }

    /// <summary>
    /// The download must go through the read-once door, not the plain one. This is the assertion
    /// that keeps the export from surviving its own download — swapping the call back to an
    /// ordinary open would leave every cut on the pod's disk, which is the thing #22 removed, and
    /// nothing else here would notice because the response looks identical.
    /// </summary>
    [Fact]
    public async Task DownloadCut_ReadsThroughTheDeleteOnCloseDoor()
    {
        _fileStorage.Setup(s => s.TryOpenReadOnce(It.IsAny<string>())).Returns(new MemoryStream([1, 2, 3]));
        var service = new Mock<ICutService>();
        service.Setup(s => s.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedCutJob("exports/deadbeef_out.mp4"));

        await CutRoutes.DownloadAsync(Guid.NewGuid(), service.Object, _fileStorage.Object, CancellationToken.None);

        _fileStorage.Verify(s => s.TryOpenReadOnce("exports/deadbeef_out.mp4"), Times.Once);
    }

    private static IDomainCutJob CompletedCutJob(string outputFilePath)
    {
        var job = new Mock<IDomainCutJob>();
        job.SetupGet(j => j.Status).Returns(JobStatus.Completed);
        job.SetupGet(j => j.OutputFilePath).Returns(outputFilePath);
        return job.Object;
    }
}
