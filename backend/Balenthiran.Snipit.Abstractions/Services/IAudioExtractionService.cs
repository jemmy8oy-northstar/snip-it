namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Extracts a mono 16kHz WAV audio track from a source video/audio file, via FFmpeg.</summary>
public interface IAudioExtractionService
{
    Task<string> ExtractAudioAsync(string sourceFilePath, string outputWavPath, CancellationToken cancellationToken = default);
}

/// <summary>Builds the FFmpeg argument list for audio extraction. Pure — no process execution.</summary>
public interface IFfmpegAudioExtractionArgumentsBuilder
{
    string[] Build(string sourceFilePath, string outputWavPath);
}
