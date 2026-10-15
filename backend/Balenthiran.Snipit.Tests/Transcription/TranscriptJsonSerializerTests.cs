using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.Services.Transcription;

namespace Balenthiran.Snipit.Tests.Transcription;

public class TranscriptJsonSerializerTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrips()
    {
        var transcript = new DomainTranscript
        {
            DurationSeconds = 12.5,
            Segments = [new DomainTranscriptSegment { Index = 0, Start = 0, End = 5, Text = "hello world" }],
            Words =
            [
                new DomainTranscriptWord { Text = "hello", Start = 0, End = 0.4, Kept = true },
                new DomainTranscriptWord { Text = "world", Start = 0.5, End = 0.9, Kept = false },
            ],
        };

        var json = TranscriptJsonSerializer.Serialize(transcript);
        var result = TranscriptJsonSerializer.Deserialize(json);

        Assert.NotNull(result);
        Assert.Equal(transcript.DurationSeconds, result!.DurationSeconds);
        Assert.Equal(transcript.Segments[0].Text, result.Segments[0].Text);
        Assert.Equal(2, result.Words.Count);
        Assert.False(result.Words[1].Kept);
    }

    [Fact]
    public void Deserialize_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(TranscriptJsonSerializer.Deserialize(null));
        Assert.Null(TranscriptJsonSerializer.Deserialize(""));
    }
}
