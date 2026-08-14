namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>
/// Body of the 429 returned when snip-it declines an upload because it is a public preview and
/// has hit a limit. A typed body rather than a bare status so the client can show the actual
/// sentence, instead of inventing its own guess at why the upload was refused.
/// </summary>
public class PreviewLimitReached
{
    public required string Message { get; set; }
}
