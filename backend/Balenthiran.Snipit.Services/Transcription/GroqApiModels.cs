using System.Text.Json.Serialization;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>Wire format for Groq's OpenAI-compatible /audio/transcriptions endpoint (verbose_json,
/// timestamp_granularities=["segment","word"]). Internal to the Groq client — never exposed at the API boundary.</summary>
public class GroqApiResponse
{
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("segments")]
    public List<GroqApiSegment> Segments { get; set; } = [];

    [JsonPropertyName("words")]
    public List<GroqApiWord> Words { get; set; } = [];
}

public class GroqApiSegment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GroqApiWord
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }
}
