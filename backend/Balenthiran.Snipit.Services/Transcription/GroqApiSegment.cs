using System.Text.Json.Serialization;

namespace Balenthiran.Snipit.Services.Transcription;

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
