using System.Text.Json;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DomainModels.Models;

namespace Balenthiran.Snipit.Services.Cutting;

/// <summary>(De)serializes keep-ranges to/from the JSON stored in CutJobEntity.KeepRangesJson.
/// Serializes the interface shape (only Start/End are public on it) and rehydrates into the
/// concrete <see cref="DomainKeepRange"/> record.</summary>
public static class KeepRangeJsonSerializer
{
    public static string Serialize(IReadOnlyList<IDomainKeepRange> ranges) => JsonSerializer.Serialize(ranges);

    public static List<DomainKeepRange> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<DomainKeepRange>>(json) ?? [];
}
