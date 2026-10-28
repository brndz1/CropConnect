using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Sensors.Service.Application;
using Sensors.Service.Protos;

namespace Sensors.Service.Services;

[Authorize]
public sealed class ThresholdsGrpcService : ThresholdsService.ThresholdsServiceBase
{
    #region Dependencies

    private readonly IThresholdApplicationService _thresholds;

    public ThresholdsGrpcService(IThresholdApplicationService thresholds)
    {
        _thresholds = thresholds;
    }

    #endregion

    #region Queries

    public override async Task<ListThresholdsResponse> ListThresholds(Empty request, ServerCallContext context)
    {
        var thresholds = await _thresholds.ListAsync(context.GetCaller(), context.CancellationToken);
        return thresholds.ToProto();
    }

    #endregion

    #region Commands

    public override async Task<ThresholdInfo> UpdateThreshold(UpdateThresholdRequest request, ServerCallContext context)
    {
        var command = new UpdateThresholdCommand(
            request.Type.ToDomain(),
            request.MinValue,
            request.MaxValue,
            RequestParsing.OptionalNumber(request.HasExpectedVersion, request.ExpectedVersion));

        var threshold = await _thresholds.UpdateAsync(context.GetCaller(), command, context.CancellationToken);
        return threshold.ToProto();
    }

    #endregion
}
