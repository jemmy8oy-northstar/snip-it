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
        using var readBack = sut.OpenRead(key);
        var buffer = new byte[3];
        Assert.Equal(3, await readBack.ReadAsync(buffer));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
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
    public void GetFullPath_AcceptsNormalKeys()
    {
        var fullPath = CreateSut().GetFullPath("cuts/abc_video.mp4");

        Assert.StartsWith(Path.GetFullPath(_root), fullPath);
    }
}
