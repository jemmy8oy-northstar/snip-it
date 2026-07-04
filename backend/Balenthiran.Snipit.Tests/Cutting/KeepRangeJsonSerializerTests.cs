using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Services.Cutting;

namespace Balenthiran.Snipit.Tests.Cutting;

public class KeepRangeJsonSerializerTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrips()
    {
        var ranges = new List<DomainKeepRange> { new(0.5, 3.2), new(6, 8.3) };

        var json = KeepRangeJsonSerializer.Serialize(ranges);
        var result = KeepRangeJsonSerializer.Deserialize(json);

        Assert.Equal(ranges, result);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyList()
    {
        Assert.Empty(KeepRangeJsonSerializer.Deserialize("[]"));
    }
}
