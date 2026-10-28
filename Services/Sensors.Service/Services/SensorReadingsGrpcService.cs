using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Sensors.Service.Application;
using Sensors.Service.Protos;
using Sensors.Service.Security;

namespace Sensors.Service.Services;

public sealed class SensorReadingsGrpcService : SensorReadingsService.SensorReadingsServiceBase
{
    #region Dependencies

    private readonly IReadingIngestionService _ingestion;
    private readonly ITelemetryQueryService _telemetry;

    public SensorReadingsGrpcService(IReadingIngestionService ingestion, ITelemetryQueryService telemetry)
    {
        _ingestion = ingestion;
        _telemetry = telemetry;
    }

    #endregion

    #region Queries

    [Authorize]
    public override async Task<ListReadingsResponse> ListReadings(ListReadingsRequest request, ServerCallContext context)
    {
        var query = new ReadingQuery(
            RequestParsing.ParseId(request.FarmId, "Farm ID"),
            request.HasType ? request.Type.ToDomain() : null,
            RequestParsing.OptionalTimestamp(request.From),
            RequestParsing.OptionalTimestamp(request.To),
            PageRequest.Create(request.Page, request.PageSize));

        var page = await _telemetry.ListReadingsAsync(context.GetCaller(), query, context.CancellationToken);
        return page.ToProto();
    }

    [Authorize]
    public override async Task<GetLatestReadingsResponse> GetLatestReadings(GetLatestReadingsRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.FarmId, "Farm ID");
        var readings = await _telemetry.GetLatestReadingsAsync(context.GetCaller(), farmId, context.CancellationToken);

        var response = new GetLatestReadingsResponse();
        response.Readings.AddRange(readings.Select(r => r.ToProto()));
        return response;
    }

    #endregion

    #region Commands

    // Readings normally arrive through RabbitMQ; this lets internal callers and tests record one directly.
    [Authorize(Policy = AuthorizationPolicies.InternalService)]
    public override async Task<RecordReadingResponse> RecordReading(RecordReadingRequest request, ServerCallContext context)
    {
        var command = new RecordReadingCommand(
            RequestParsing.ParseId(request.MessageId, "Message ID"),
            RequestParsing.ParseId(request.FarmId, "Farm ID"),
            request.SensorId,
            request.Type.ToDomain(),
            request.Value,
            RequestParsing.RequiredTimestamp(request.Timestamp, "Timestamp"));

        var result = await _ingestion.RecordAsync(command, context.CancellationToken);
        return result.ToProto();
    }

    #endregion
}
