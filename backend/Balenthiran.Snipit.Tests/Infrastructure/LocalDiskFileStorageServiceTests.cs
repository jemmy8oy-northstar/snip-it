using Balenthiran.Snipit.Services.Infrastructure;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Tests.Infrastructure;

public class LocalDiskFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"snipit-storage-tests-{Guid.NewGuid():N}");

    private LocalDiskFileStorageService CreateSut() =>
        new(Options.Create(new FileStorageOptions { RootPath = _root }));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesFileUnderRootAndReturnsReadableKey()
    {
        var sut = CreateSut();
        using var content = new MemoryStream([1, 2, 3]);

        var key = await sut.SaveAsync(content, "uploads", "video.mp4");

        Assert.StartsWith("uploads/", key);
        Assert.EndsWith("_video.mp4", key);
        using var readBack = sut.TryOpenReadOnce(key);
        Assert.NotNull(readBack);
        var buffer = new byte[3];
        Assert.Equal(3, await readBack.ReadAsync(buffer));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
    }

    /// <summary>
    /// The property the whole of #22 rests on: a file read through this door does not survive the
    /// read. If this regresses to an ordinary open, every exported cut stays on the pod's disk
    /// forever and *every other test in this file still passes* — the bytes come back either way.
    /// </summary>
    [Fact]
    public async Task TryOpenReadOnce_DeletesTheFileWhenTheStreamIsDisposed()
    {
        var sut = CreateSut();
        using var content = new MemoryStream([1, 2, 3]);
        var key = await sut.SaveAsync(content, "exports", "cut.mp4");
        var fullPath = sut.GetFullPath(key);

        // Still there while the stream is open — a download must be able to finish reading.
        var stream = sut.TryOpenReadOnce(key);
        Assert.NotNull(stream);
        Assert.True(File.Exists(fullPath), "the file must survive until the response has been written");

        await stream.DisposeAsync();

        Assert.False(File.Exists(fullPath), "disposing the stream is what deletes the export");
    }

    [Fact]
    public async Task Delete_RemovesTheFile()
    {
        var sut = CreateSut();
        using var content = new MemoryStream([1]);
        var key = await sut.SaveAsync(content, "uploads", "video.mp4");

        sut.Delete(key);

        Assert.False(File.Exists(sut.GetFullPath(key)));
    }

    [Fact]
    public void Delete_IsSilentWhenTheFileAndItsFolderAreAlreadyGone()
    {
        // This runs in finally blocks. A job that failed before writing anything must not have its
        // real exception replaced by a cleanup one — that would hide every genuine failure.
        var sut = CreateSut();

        sut.Delete("uploads/never_written.mp4");
    }

    [Fact]
    public void Delete_StillThrowsForKeysResolvingOutsideRoot()
    {
        // Deleting by an unvalidated key is the one operation where being forgiving is dangerous.
        Assert.Throws<InvalidOperationException>(() => CreateSut().Delete("../../outside.txt"));
    }

    /// <summary>
    /// With the volume gone (#22), nothing sets FileStorage__RootPath in the cluster any more, so
    /// the fallback is what actually runs in production. It has to land on ephemeral disk rather
    /// than next to the application binaries.
    /// </summary>
    [Fact]
    public void RootPath_DefaultsToScratchUnderTheTempPathWhenUnconfigured()
    {
        var sut = new LocalDiskFileStorageService(Options.Create(new FileStorageOptions()));

        var fullPath = sut.GetFullPath("exports/abc.mp4");

        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), fullPath);
        Assert.DoesNotContain(AppContext.BaseDirectory, fullPath);
    }

    [Theory]
    [InlineData("../../../etc/evil.txt")]
    [InlineData("..\\..\\evil.txt")]
    [InlineData("/etc/evil.txt")]
    public async Task SaveAsync_NeutralisesPathTraversalInUploadFileName(string maliciousName)
    {
        var sut = CreateSut();
        using var content = new MemoryStream([1]);

        var key = await sut.SaveAsync(content, "uploads", maliciousName);

        var fullPath = sut.GetFullPath(key);
        Assert.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, fullPath);
        Assert.DoesNotContain("..", key);
    }

    [Fact]
    public async Task SaveAsync_FallsBackToSyntheticNameWhenNothingSurvivesSanitising()
    {
        var sut = CreateSut();
        using var content = new MemoryStream([1]);

        var key = await sut.SaveAsync(content, "uploads", "..");

        Assert.EndsWith("_upload", key);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("uploads/../../outside.txt")]
    public void GetFullPath_RejectsKeysResolvingOutsideRoot(string escapingKey)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateSut().GetFullPath(escapingKey));
        Assert.Contains("outside the storage root", ex.Message);
    }

    [Fact]
    public void TryOpenReadOnce_ReturnsNullWhenTheFileIsGoneButItsFolderRemains()
    {
        var sut = CreateSut();
        Directory.CreateDirectory(Path.Combine(_root, "uploads"));

        Assert.Null(sut.TryOpenReadOnce("uploads/never_written.mp4"));
    }

    [Fact]
    public void TryOpenReadOnce_ReturnsNullWhenTheWholeStorageRootIsGone()
    {
        // What a restarted pod behind a surviving database looks like. This is a different
        // exception from the case above (DirectoryNotFoundException, not FileNotFoundException),
        // so catching only the obvious one would still crash here.
        Assert.Null(CreateSut().TryOpenReadOnce("uploads/never_written.mp4"));
    }

    [Fact]
    public void TryOpenReadOnce_StillThrowsForKeysResolvingOutsideRoot()
    {
        // Not "missing" — a broken caller or an attack. Answering null would file it under
        // "nothing there" and hide it.
        Assert.Throws<InvalidOperationException>(() => CreateSut().TryOpenReadOnce("../../outside.txt"));
    }

    [Fact]
    public void GetFullPath_AcceptsNormalKeys()
    {
        var fullPath = CreateSut().GetFullPath("cuts/abc_video.mp4");

        Assert.StartsWith(Path.GetFullPath(_root), fullPath);
    }
}
