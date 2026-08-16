namespace Balenthiran.Snipit.Services.Preview;

/// <summary>Everything about how snip-it behaves as a public preview, in one place.</summary>
public class PreviewOptions
{
    public const string SectionName = "Preview";

    /// <summary>
    /// Site-wide transcriptions accepted per UTC day. Not per visitor — there is no sign-in, so
    /// there is nobody to count. Groq's free tier allows far more than this; the cap exists to
    /// keep an unattended public app's worst day bounded and predictable, not to ration it.
    /// </summary>
    public int MaxTranscriptionsPerDay { get; set; } = 25;

    /// <summary>
    /// How long to stop accepting uploads after the provider says we are rate limited, when it
    /// does not tell us itself via <c>Retry-After</c>.
    /// </summary>
    public TimeSpan UpstreamLimitCooldown { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Shown when the daily cap is reached. Written for a visitor, not an operator.</summary>
    public string DailyLimitMessage { get; set; } =
        "snip-it is a preview, and it has already done today's batch of transcriptions. "
        + "Try again tomorrow — everything you have already cut is still here.";

    /// <summary>Shown when the transcription provider itself is rate limiting us.</summary>
    public string UpstreamLimitMessage { get; set; } =
        "snip-it is a preview and it is briefly out of transcription capacity. "
        + "Give it a few minutes and try again — nothing you have uploaded is lost.";
}
