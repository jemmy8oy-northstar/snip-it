namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Builds the FFmpeg argument list for audio extraction. Pure — no process execution.</summary>
public interface IFfmpegAudioExtractionArgumentsBuilder
{
    string[] Build(string sourceFilePath, string outputWavPath);
}
