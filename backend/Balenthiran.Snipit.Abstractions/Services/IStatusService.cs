using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;

namespace Balenthiran.Snipit.Abstractions.Services;

public interface IStatusService
{
    Task<IDomainStatus> GetSystemStatusAsync();
}
