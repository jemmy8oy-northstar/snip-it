using Balenthiran.Snipit.Services.Infrastructure;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Tests.Infrastructure;

/// <summary>BEH-EDITION-3: a saved take lives on the takes volume, beside its production.</summary>
public class LocalDiskTakeStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"snipit-take-tests-{Guid.NewGuid():N}");
    private readonly Guid _production = Guid.NewGuid();

    private LocalDiskTakeStore CreateSut(long maxTotalBytes = 1024, string? root = null) =>
        new(Options.Create(new TakeStoreOptions { RootPath = root ?? _root, MaxTotalBytes = maxTotalBytes }));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MemoryStream Bytes(int count) => new(new byte[count]);

    [Fact]
    public void WithNoRootConfigured_SavingIsOff()
    {
        Assert.False(CreateSut(root: "").Enabled);
        Assert.True(CreateSut().Enabled);
    }

    [Fact]
    public async Task Save_StoresTheTakeUnderItsProductionAndItReadsBack()
    {
        var sut = CreateSut();

        var saved = await sut.SaveAsync(_production, "take-1.mov", new MemoryStream([1, 2, 3]));

        Assert.Equal("take-1.mov", saved!.Name);
        Assert.Equal(3, saved.Bytes);
        Assert.True(File.Exists(Path.Combine(_root, _production.ToString("D"), "take-1.mov")));
        using var read = sut.TryOpen(_production, "take-1.mov");
        Assert.NotNull(read);
        var buffer = new byte[3];
        Assert.Equal(3, await read.ReadAsync(buffer));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
    }

    [Fact]
    public async Task List_ReturnsOnlyThatProductionsTakes()
    {
        var sut = CreateSut();
        await sut.SaveAsync(_production, "take-1.mov", Bytes(1));
        await sut.SaveAsync(_production, "take-2.mp4", Bytes(1));
        await sut.SaveAsync(Guid.NewGuid(), "take-9.mov", Bytes(1));

        var names = sut.List(_production).Select(t => t.Name).Order().ToList();

        Assert.Equal(["take-1.mov", "take-2.mp4"], names);
    }

    [Fact]
    public void List_ForAProductionWithNothingSaved_IsEmptyNotAnError() =>
        Assert.Empty(CreateSut().List(_production));

    [Fact]
    public async Task SavingTheSameTakeTwice_ReplacesItAndCountsItOnce()
    {
        var sut = CreateSut(maxTotalBytes: 100);
        await sut.SaveAsync(_production, "take-1.mov", Bytes(80));

        var again = await sut.SaveAsync(_production, "take-1.mov", Bytes(90));

        Assert.NotNull(again);
        Assert.Equal(90, again.Bytes);
        Assert.Single(sut.List(_production));
    }

    [Fact]
    public async Task Save_PastTheTotalCap_StoresNothingAndLeavesNoPartialFile()
    {
        var sut = CreateSut(maxTotalBytes: 100);
        await sut.SaveAsync(_production, "take-1.mov", Bytes(80));

        var refused = await sut.SaveAsync(_production, "take-2.mov", Bytes(30));

        Assert.Null(refused);
        Assert.Null(sut.TryOpen(_production, "take-2.mov"));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, _production.ToString("D"))));
    }

    [Theory]
    [InlineData("../escape.mov")]
    [InlineData("..")]
    [InlineData("a/b.mov")]
    [InlineData("a\\b.mov")]
    [InlineData(".hidden")]
    [InlineData("a..b.mov")]
    [InlineData("")]
    [InlineData("take 1.mov")]
    public async Task AnInvalidName_IsRefusedBeforeAnyPathIsBuilt(string name)
    {
        var sut = CreateSut();

        Assert.False(sut.IsValidName(name));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SaveAsync(_production, name, Bytes(1)));
        Assert.Throws<ArgumentException>(() => sut.TryOpen(_production, name));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void ANameOverTheLengthCap_IsInvalid() =>
        Assert.False(CreateSut().IsValidName(new string('a', 101)));

    [Fact]
    public async Task TryOpen_ForATakeThatIsNotThere_IsNull()
    {
        var sut = CreateSut();
        await sut.SaveAsync(_production, "take-1.mov", Bytes(1));

        Assert.Null(sut.TryOpen(_production, "take-2.mov"));
        Assert.Null(sut.TryOpen(Guid.NewGuid(), "take-1.mov"));
    }
}
