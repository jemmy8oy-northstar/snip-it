using System.Net;
using System.Net.Http.Headers;
using Balenthiran.Snipit.Services.Transcription;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Tests.Transcription;

public class GroqTranscriptionClientTests
{
    private const string SampleResponseJson = """
        {
          "task": "transcribe",
          "language": "english",
          "duration": 12.5,
          "text": "hello world",
          "segments": [ { "id": 0, "start": 0.0, "end": 5.2, "text": "hello world" } ],
          "words": [
            { "word": "hello", "start": 0.0, "end": 0.4 },
            { "word": "world", "start": 0.5, "end": 0.9 }
          ]
        }
        """;

    [Fact]
    public void MapToResult_MapsSegmentsAndWordsWithAllWordsKeptByDefault()
    {
        var apiResponse = System.Text.Json.JsonSerializer.Deserialize<GroqApiResponse>(SampleResponseJson)!;

        var result = GroqTranscriptionClient.MapToResult(apiResponse);

        Assert.Equal(12.5, result.DurationSeconds);
        Assert.Single(result.Segments);
        Assert.Equal("hello world", result.Segments[0].Text);
        Assert.Equal(2, result.Words.Count);
        Assert.Equal("hello", result.Words[0].Text);
        Assert.True(result.Words[0].Kept);
        Assert.True(result.Words[1].Kept);
    }

    [Fact]
    public async Task TranscribeAsync_SendsBearerTokenAndParsesResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleResponseJson),
            };
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GroqOptions { ApiKey = "test-key", BaseUrl = "https://api.groq.test/v1" });
        var sut = new GroqTranscriptionClient(httpClient, options);

        using var audio = new MemoryStream([1, 2, 3]);
        var result = await sut.TranscribeAsync(audio, "clip.wav");

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://api.groq.test/v1/audio/transcriptions", capturedRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", capturedRequest.Headers.Authorization.Parameter);
        Assert.Equal(2, result.Words.Count);
    }

    [Fact]
    public async Task TranscribeAsync_ThrowsOnNonSuccessStatus()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("invalid api key"),
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GroqOptions { ApiKey = "bad-key" });
        var sut = new GroqTranscriptionClient(httpClient, options);

        using var audio = new MemoryStream([1]);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.TranscribeAsync(audio, "clip.wav"));
        Assert.Contains("invalid api key", ex.Message);
    }

    private class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
