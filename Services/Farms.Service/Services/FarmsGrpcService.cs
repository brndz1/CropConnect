using Farms.Service.Application;
using Farms.Service.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Farms.Service.Services;

[Authorize]
public sealed class FarmsGrpcService : FarmsService.FarmsServiceBase
{
    #region Dependencies

    private readonly IFarmApplicationService _farms;

    public FarmsGrpcService(IFarmApplicationService farms)
    {
        _farms = farms;
    }

    #endregion

    #region Queries

    [AllowAnonymous]
    public override async Task<FarmInfo> GetFarm(GetFarmRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.Id, "Farm ID");
        var farm = await _farms.GetAsync(context.GetCaller(), farmId, context.CancellationToken);
        return farm.ToProto();
    }

    [AllowAnonymous]
    public override async Task<ListFarmsResponse> ListFarms(ListFarmsRequest request, ServerCallContext context)
    {
        var query = new FarmQuery(
            PageRequest.Create(request.Page, request.PageSize),
            request.IncludeInactive,
            RequestParsing.OptionalText(request.HasSearch, request.Search),
            RequestParsing.ParseOptionalId(request.HasOwnerId, request.OwnerId, "Owner ID"));

        var page = await _farms.ListAsync(context.GetCaller(), query, context.CancellationToken);
        return page.ToProto();
    }

    public override async Task<ListFarmsResponse> ListMyFarms(ListMyFarmsRequest request, ServerCallContext context)
    {
        var page = await _farms.ListMineAsync(
            context.GetCaller(),
            PageRequest.Create(request.Page, request.PageSize),
            context.CancellationToken);

        return page.ToProto();
    }

    #endregion

    #region Commands

    public override async Task<FarmInfo> CreateFarm(CreateFarmRequest request, ServerCallContext context)
    {
        var command = new CreateFarmCommand(request.Name, request.Description, request.Location);
        var farm = await _farms.CreateAsync(context.GetCaller(), command, context.CancellationToken);
        return farm.ToProto();
    }

    public override async Task<FarmInfo> UpdateFarm(UpdateFarmRequest request, ServerCallContext context)
    {
        var command = new UpdateFarmCommand(
            RequestParsing.ParseId(request.Id, "Farm ID"),
            RequestParsing.OptionalText(request.HasName, request.Name),
            RequestParsing.OptionalText(request.HasDescription, request.Description),
            RequestParsing.OptionalText(request.HasLocation, request.Location),
            RequestParsing.OptionalNumber(request.HasExpectedVersion, request.ExpectedVersion));

        var farm = await _farms.UpdateAsync(context.GetCaller(), command, context.CancellationToken);
        return farm.ToProto();
    }

    public override async Task<FarmInfo> SetFarmActive(SetFarmActiveRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.Id, "Farm ID");
        var farm = await _farms.SetActiveAsync(context.GetCaller(), farmId, request.IsActive, context.CancellationToken);
        return farm.ToProto();
    }

    public override async Task<Empty> DeleteFarm(DeleteFarmRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.Id, "Farm ID");
        await _farms.DeleteAsync(context.GetCaller(), farmId, context.CancellationToken);
        return new Empty();
    }

    #endregion
}
