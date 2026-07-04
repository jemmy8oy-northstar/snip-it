namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>Thin abstraction over launching an external process (FFmpeg), so it can be mocked in tests.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, string[] arguments, CancellationToken cancellationToken = default);
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
