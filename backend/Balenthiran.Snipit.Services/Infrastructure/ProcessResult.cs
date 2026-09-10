using Balenthiran.Snipit.Abstractions.Services;

namespace Balenthiran.Snipit.Services.Infrastructure;

/// <summary>Concrete result of an external-process run. Implements <see cref="IProcessResult"/>
/// directly — the positional record's generated get-only members satisfy the interface.</summary>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError) : IProcessResult;
