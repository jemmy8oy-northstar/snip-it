using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DataModels.Models;

namespace Balenthiran.Snipit.DomainModels.Models;

public class DomainStatus : Status, IDomainStatus
{
    public string GetFriendlyStatus()
    {
        return $"System is running version {Version} (Updated: {LastUpdated:g})";
    }
}
