namespace Balenthiran.Snipit.DataModels.Models;

/// <summary>A take saved to snip-it's server (BEH-EDITION-3). <c>Name</c> is the file name the phone gave it.</summary>
public record SavedTake(string Name, long Bytes, DateTime SavedAt);
