namespace Notifications.Service.Application.Clients;

public interface IFarmDirectoryClient
{
    Task<string> GetFarmNameAsync(Guid farmId, CancellationToken cancellationToken);
}
