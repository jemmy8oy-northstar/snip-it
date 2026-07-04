using System.Net.Http.Headers;
using System.Text.Json;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>Calls Groq's whisper-large-v3 transcription endpoint and maps the response into the domain shape.</summary>
public class GroqTranscriptionClient(HttpClient httpClient, IOptions<GroqOptions> options) : IGroqTranscriptionClient
{
    private readonly GroqOptions _options = options.Value;

    public async Task<GroqTranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(_options.Model), "model");
        content.Add(new StringContent("verbose_json"), "response_format");
        content.Add(new StringContent("word"), "timestamp_granularities[]");
        content.Add(new StringContent("segment"), "timestamp_granularities[]");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/audio/transcriptions")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Groq transcription request failed ({(int)response.StatusCode}): {body}");
        }

        var apiResponse = JsonSerializer.Deserialize<GroqApiResponse>(body)
            ?? throw new InvalidOperationException("Groq transcription response could not be parsed.");

        return MapToResult(apiResponse);
    }

    internal static GroqTranscriptionResult MapToResult(GroqApiResponse apiResponse) => new()
    {
        DurationSeconds = apiResponse.Duration,
        Segments = apiResponse.Segments
            .Select(s => new DomainTranscriptSegment { Index = s.Id, Start = s.Start, End = s.End, Text = s.Text })
            .ToList(),
        Words = apiResponse.Words
            .Select(w => new DomainTranscriptWord { Text = w.Word, Start = w.Start, End = w.End, Kept = true })
            .ToList(),
    };
}
