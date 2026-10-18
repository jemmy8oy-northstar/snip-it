using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DomainModels.Models;
using Microsoft.Extensions.Options;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>Calls Groq's whisper-large-v3 transcription endpoint and maps the response into the domain shape.</summary>
public class GroqTranscriptionClient(HttpClient httpClient, IOptions<GroqOptions> options) : IGroqTranscriptionClient
{
    private readonly GroqOptions _options = options.Value;

    public async Task<IGroqTranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default)
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

        if (response.StatusCode == HttpStatusCode.TooManyRequests || IsQuotaExhausted(response.StatusCode, body))
        {
            // The body is provider JSON quoting our own account's limits and usage. It goes in the
            // log, never in the exception message — that message ends up on a public page.
            throw new TranscriptionQuotaExceededException(
                $"Transcription provider is rate limiting us ({(int)response.StatusCode}).",
                RetryAfterFrom(response));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Groq transcription request failed ({(int)response.StatusCode}): {body}");
        }

        var apiResponse = JsonSerializer.Deserialize<GroqApiResponse>(body)
            ?? throw new InvalidOperationException("Groq transcription response could not be parsed.");

        return MapToResult(apiResponse);
    }

    /// <summary>
    /// Groq (like OpenAI, whose API shape it copies) signals a spent daily allowance with 429, but
    /// a billing-level exhaustion arrives as 403 with a <c>*_quota_exceeded</c> code in the body.
    /// Both mean "come back later", and both must read as a preview limit rather than a crash.
    /// </summary>
    internal static bool IsQuotaExhausted(HttpStatusCode statusCode, string body) =>
        (statusCode == HttpStatusCode.Forbidden || statusCode == HttpStatusCode.PaymentRequired)
        && (body.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || body.Contains("rate_limit", StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads <c>Retry-After</c> in either of its legal forms: delta-seconds or an HTTP date.</summary>
    internal static TimeSpan? RetryAfterFrom(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        return retryAfter.Date is { } date && date > DateTimeOffset.UtcNow
            ? date - DateTimeOffset.UtcNow
            : null;
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
