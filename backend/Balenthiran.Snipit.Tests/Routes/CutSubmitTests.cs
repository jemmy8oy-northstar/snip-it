using AutoMapper;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.WebApi.Routes;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Balenthiran.Snipit.Tests.Routes;

/// <summary>
/// The cut request arrives as a JSON string inside a form field, so it is not in openapi.json and
/// the editor builds it by hand. The field name is therefore a seam no generated type pins: a
/// rename on either side would silently drop pause-cutting back to "keep every pause".
/// </summary>
public class CutSubmitTests
{
    private const string JobId = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("""{"transcriptionJobId":"00000000-0000-0000-0000-000000000001","words":[],"maxPauseSeconds":0.7}""", 0.7)]
    [InlineData("""{"transcriptionJobId":"00000000-0000-0000-0000-000000000001","words":[]}""", null)]
    public async Task Submit_PassesMaxPauseSecondsFromTheJsonFieldToTheService(string request, double? expected)
    {
        double? received = -1;
        var service = new Mock<ICutService>();
        service.Setup(s => s.SubmitAsync(Guid.Parse(JobId), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<IDomainTranscriptWord>>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, IReadOnlyList<IDomainTranscriptWord>, double?, CancellationToken>(
                (_, _, _, maxPause, _) => received = maxPause)
            .ThrowsAsync(new InvalidOperationException("stop before building a response"));
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<List<DomainTranscriptWord>>(It.IsAny<object>())).Returns([]);
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cut-sources/x.mp4");
        var file = new FormFile(new MemoryStream([1]), 0, 1, "file", "take.mov");

        await CutRoutes.SubmitAsync(file, request, service.Object, storage.Object, mapper.Object,
            new DefaultHttpContext(), CancellationToken.None);

        Assert.Equal(expected, received);
    }
}
