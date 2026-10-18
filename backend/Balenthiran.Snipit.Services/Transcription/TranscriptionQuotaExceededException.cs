namespace Balenthiran.Snipit.Services.Transcription;

/// <summary>
/// The transcription provider refused the request because we are out of quota or going too fast.
/// Distinct from a generic failure because it is the one case that is nobody's fault, is
/// temporary, and must be reported to a visitor in words rather than as a stack of provider JSON.
/// </summary>
public class TranscriptionQuotaExceededException(string message, TimeSpan? retryAfter = null)
    : Exception(message)
{
    /// <summary>Provider-supplied cooldown from the <c>Retry-After</c> header, when it sends one.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
