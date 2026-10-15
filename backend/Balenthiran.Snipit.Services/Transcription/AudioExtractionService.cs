using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Transcription;

public class AudioExtractionService(IFfmpegAudioExtractionArgumentsBuilder argumentsBuilder, IProcessRunner processRunner)
    : IAudioExtractionService
{
    public async Task<string> ExtractAudioAsync(string sourceFilePath, string outputWavPath, CancellationToken cancellationToken = default)
    {
        var args = argumentsBuilder.Build(sourceFilePath, outputWavPath);
        var result = await processRunner.RunAsync("ffmpeg", args, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg audio extraction failed (exit code {result.ExitCode}): {result.StandardError}");
        }

        return outputWavPath;
    }
}
