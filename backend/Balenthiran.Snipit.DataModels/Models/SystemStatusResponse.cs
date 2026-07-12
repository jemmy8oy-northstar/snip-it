namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>Health/status payload. A dedicated response type because the route projects a
/// shape (adds the friendly string) meaningfully different from the raw status the service returns.</summary>
public record SystemStatusResponse(string Version, string FriendlyStatus, DateTime Timestamp);
