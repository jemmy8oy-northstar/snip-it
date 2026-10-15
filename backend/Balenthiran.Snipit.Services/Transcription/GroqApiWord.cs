using System.Text.Json.Serialization;

namespace Balenthiran.Snipit.Services.Transcription;

public class GroqApiWord
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }
}
