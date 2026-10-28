using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Sensors.Service.Application;
using Sensors.Service.Protos;

namespace Sensors.Service.Services;

[Authorize]
public sealed class AlertsGrpcService : AlertsService.AlertsServiceBase
{
    #region Dependencies

    private readonly ITelemetryQueryService _telemetry;

    public AlertsGrpcService(ITelemetryQueryService telemetry)
    {
        _telemetry = telemetry;
    }

    #endregion

    #region Queries

    public override async Task<ListAlertsResponse> ListAlerts(ListAlertsRequest request, ServerCallContext context)
    {
        var query = new AlertQuery(
            RequestParsing.ParseId(request.FarmId, "Farm ID"),
            request.OnlyOpen,
            RequestParsing.OptionalTimestamp(request.From),
            RequestParsing.OptionalTimestamp(request.To),
            PageRequest.Create(request.Page, request.PageSize));

        var page = await _telemetry.ListAlertsAsync(context.GetCaller(), query, context.CancellationToken);
        return page.ToProto();
    }

    #endregion

    #region Commands

    public override async Task<AlertInfo> AcknowledgeAlert(AcknowledgeAlertRequest request, ServerCallContext context)
    {
        var alertId = RequestParsing.ParseId(request.Id, "Alert ID");
        var alert = await _telemetry.AcknowledgeAlertAsync(context.GetCaller(), alertId, context.CancellationToken);
        return alert.ToProto();
    }

    #endregion
}
