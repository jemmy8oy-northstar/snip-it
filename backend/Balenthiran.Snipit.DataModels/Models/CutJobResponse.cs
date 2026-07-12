namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>CutJob plus a download URL, populated by the route once the job is Completed.</summary>
public class CutJobResponse : CutJob
{
    public string? DownloadUrl { get; set; }
}
