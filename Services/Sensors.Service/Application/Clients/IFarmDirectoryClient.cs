namespace Sensors.Service.Application.Clients;

public sealed record FarmOwnership(
    Guid FarmId,
    Guid OwnerId,
    string Name,
    bool IsActive,
    double? Latitude = null,
    double? Longitude = null);

public interface IFarmDirectoryClient
{
    Task<FarmOwnership> GetOwnershipAsync(Guid farmId, CancellationToken cancellationToken);
}
