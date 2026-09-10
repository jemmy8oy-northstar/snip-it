namespace Balenthiran.Snipit.Abstractions.Services;

public interface IProcessResult
{
    int ExitCode { get; }
    string StandardOutput { get; }
    string StandardError { get; }
}
