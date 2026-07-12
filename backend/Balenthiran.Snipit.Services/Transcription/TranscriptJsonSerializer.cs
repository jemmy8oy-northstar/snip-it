using System.Text.Json;
using Balenthiran.Snipit.DomainModels.Models;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>(De)serializes a <see cref="DomainTranscript"/> to/from the JSON stored in TranscriptionJobEntity.TranscriptJson.</summary>
public static class TranscriptJsonSerializer
{
    public static string Serialize(DomainTranscript transcript) => JsonSerializer.Serialize(transcript);

    public static DomainTranscript? Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<DomainTranscript>(json);
}
