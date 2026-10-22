namespace Balenthiran.Snipit.Services.Infrastructure;

public class TakeStoreOptions
{
    public const string SectionName = "Takes";

    /// <summary>
    /// Directory on the takes volume (BEH-EDITION-3). Empty means saving takes is off, which is the
    /// default: without a volume behind it a "saved" take would vanish on the next restart, and
    /// that is worse than refusing. The chart sets it only when <c>takes.persistence.enabled</c>.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Most bytes all saved takes may occupy. The routes are open like the rest of snip-it (#13),
    /// so this is what stops a stranger filling the volume. Default 40 GiB, under the 50 GiB
    /// OCI block-volume minimum so the filesystem never reaches full.
    /// </summary>
    public long MaxTotalBytes { get; set; } = 40L * 1024 * 1024 * 1024;
}
