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

    public SensorReadingsGrpcService(IReadingIngestionService ingestion)
    {
        _ingestion = ingestion;
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
