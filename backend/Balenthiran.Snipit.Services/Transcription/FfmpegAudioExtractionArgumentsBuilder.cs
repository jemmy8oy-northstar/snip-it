using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>
/// Builds the FFmpeg args to extract mono 16kHz PCM WAV audio — the format Whisper expects —
/// from any source video/audio file.
/// </summary>
public class FfmpegAudioExtractionArgumentsBuilder : IFfmpegAudioExtractionArgumentsBuilder
{
    public string[] Build(string sourceFilePath, string outputWavPath) =>
    [
        "-hide_banner",
        "-y",
        "-i", sourceFilePath,
        "-vn",
        "-ac", "1",
        "-ar", "16000",
        "-acodec", "pcm_s16le",
        outputWavPath,
    ];
}
