using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.DomainModels.Models;

namespace Balenthiran.Snipit.Services;

public class StatusService : IStatusService
{
    public Task<IDomainStatus> GetSystemStatusAsync()
    {
        IDomainStatus model = new DomainStatus
        {
            Version = "1.1.0-alpha",
            LastUpdated = DateTime.UtcNow
        };
        
        return Task.FromResult(model);
    }
}
