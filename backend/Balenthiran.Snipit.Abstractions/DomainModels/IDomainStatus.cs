namespace Balenthiran.Snipit.Abstractions.DomainModels;

using Balenthiran.Snipit.Abstractions.DataModels;

public interface IDomainStatus : IStatus
{
    string GetFriendlyStatus();
}
