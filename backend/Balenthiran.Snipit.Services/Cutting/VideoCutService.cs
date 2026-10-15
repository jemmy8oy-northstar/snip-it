using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Cutting;

public class VideoCutService(IFfmpegCutArgumentsBuilder argumentsBuilder, IProcessRunner processRunner) : IVideoCutService
{
    public async Task<string> CutAsync(string sourceFilePath, IReadOnlyList<IDomainKeepRange> keepRanges, string outputFilePath, CancellationToken cancellationToken = default)
    {
        var args = argumentsBuilder.Build(sourceFilePath, keepRanges, outputFilePath);
        var result = await processRunner.RunAsync("ffmpeg", args, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg cut failed (exit code {result.ExitCode}): {result.StandardError}");
        }

        return outputFilePath;
    }
}
