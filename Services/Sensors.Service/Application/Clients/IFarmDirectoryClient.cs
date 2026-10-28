namespace Sensors.Service.Application.Clients;

public sealed record FarmOwnership(Guid FarmId, Guid OwnerId, string Name, bool IsActive);

public interface IFarmDirectoryClient
{
    Task<FarmOwnership> GetOwnershipAsync(Guid farmId, CancellationToken cancellationToken);
}
