using Farms.Service.Application;
using Farms.Service.Protos;
using Farms.Service.Security;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Farms.Service.Services;

[Authorize(Policy = AuthorizationPolicies.InternalService)]
public sealed class FarmDirectoryGrpcService : FarmDirectoryService.FarmDirectoryServiceBase
{
    #region Dependencies

    private readonly IFarmApplicationService _farms;

    public FarmDirectoryGrpcService(IFarmApplicationService farms)
    {
        _farms = farms;
    }

    #endregion

    #region Queries

    public override async Task<FarmOwnershipInfo> GetFarmOwnership(GetFarmOwnershipRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.FarmId, "Farm ID");
        var farm = await _farms.GetForInternalUseAsync(farmId, context.CancellationToken);

        return new FarmOwnershipInfo
        {
            FarmId = farm.Id.ToString(),
            OwnerId = farm.OwnerId.ToString(),
            Name = farm.Name,
            IsActive = farm.IsActive
        };
    }

    #endregion
}
