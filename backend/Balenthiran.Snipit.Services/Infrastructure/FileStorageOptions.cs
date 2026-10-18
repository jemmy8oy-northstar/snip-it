namespace Balenthiran.Snipit.Services.Infrastructure;

public class FileStorageOptions
{
    /// <summary>Absolute path to the root storage directory. Defaults to App_Data/storage under the app base directory.</summary>
    public string RootPath { get; set; } = string.Empty;
}
