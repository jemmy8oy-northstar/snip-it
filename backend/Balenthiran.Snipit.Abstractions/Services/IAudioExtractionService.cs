namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Extracts a mono 16kHz WAV audio track from a source video/audio file, via FFmpeg.</summary>
public interface IAudioExtractionService
{
    Task<string> ExtractAudioAsync(string sourceFilePath, string outputWavPath, CancellationToken cancellationToken = default);
}
