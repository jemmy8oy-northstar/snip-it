namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>CutJob plus a download URL, populated by the route once the job is Completed.</summary>
public class CutJobResponse : CutJob
{
    /// <summary>Null until the job completes, then an absolute path a client can fetch.</summary>
    public required string? DownloadUrl { get; set; }
}
