namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>CutJobDto plus a download URL, populated by the route once the job is Completed.</summary>
public class CutJobResponse : CutJobDto
{
    public string? DownloadUrl { get; set; }
}
