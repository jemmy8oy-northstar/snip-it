using System.Text.Json;
using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Services.Cutting;

/// <summary>(De)serializes keep-ranges to/from the JSON stored in CutJobEntity.KeepRangesJson.</summary>
public static class KeepRangeJsonSerializer
{
    public static string Serialize(List<DomainKeepRange> ranges) => JsonSerializer.Serialize(ranges);

    public static List<DomainKeepRange> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<DomainKeepRange>>(json) ?? [];
}
