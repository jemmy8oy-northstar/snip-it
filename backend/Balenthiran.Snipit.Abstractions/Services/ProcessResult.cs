namespace Balenthiran.Snipit.Abstractions.Services;

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
