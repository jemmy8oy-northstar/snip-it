using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace Balenthiran.Snipit.Tests;

/// <summary>Builds a real <see cref="IMapper"/> from the Services entity→domain profile so
/// service tests exercise the same mapping wired up in production.</summary>
internal static class TestMapper
{
    public static IMapper Create() =>
        new MapperConfiguration(
            cfg => cfg.AddProfile<Balenthiran.Snipit.Services.Mapper>(),
            NullLoggerFactory.Instance).CreateMapper();
}
