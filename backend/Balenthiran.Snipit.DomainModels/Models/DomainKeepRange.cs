using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.DomainModels.Models;

public record DomainKeepRange(double Start, double End) : IDomainKeepRange;
