namespace Balenthiran.Snipit.Abstractions.Services;

/// <summary>
/// snip-it is open to anyone, so the only thing standing between a visitor and James's
/// transcription bill is this. Answers "may we accept another upload right now?", and
/// records the moment the upstream provider tells us we are out of quota.
/// </summary>
public interface IPreviewQuotaService
{
    /// <summary>
    /// Null when an upload may proceed; otherwise the message to show the visitor.
    /// </summary>
    Task<string?> GetBlockReasonAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the transcription provider rejects a job for rate-limit or quota reasons.
    /// Stops us accepting uploads we already know we cannot process.
    /// </summary>
    /// <param name="retryAfter">Provider-supplied cooldown, when it sends one.</param>
    void RecordUpstreamLimitHit(TimeSpan? retryAfter = null);
}
