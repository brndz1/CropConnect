using CropConnect.ApiGateway.Infrastructure;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http.HttpResults;
using Sensors.Service.Protos;
using ProtoSensorType = Sensors.Service.Protos.SensorType;

namespace CropConnect.ApiGateway.Endpoints;

// RF04, RF17, RF18: alert thresholds, readings and alerts (Sensors.Service). Readings themselves
// arrive from Node-RED through RabbitMQ (RF16), so there is no public endpoint to record one.
public static class SensorEndpoints
{
    #region Contracts

    public enum SensorType
    {
        Humidity,
        Temperature
    }

    public sealed record ThresholdResponse(SensorType? Type, double MinValue, double MaxValue, string Unit, DateTime? UpdatedAt, int Version);

    public sealed record UpdateThresholdBody(double MinValue, double MaxValue, int? ExpectedVersion);

    public sealed record ReadingResponse(
        string Id,
        string FarmId,
        string SensorId,
        SensorType? Type,
        double Value,
        string Unit,
        DateTime? Timestamp,
        DateTime? ReceivedAt);

    public sealed record AlertResponse(
        string Id,
        string FarmId,
        SensorType? Type,
        double Value,
        double MinValue,
        double MaxValue,
        string Unit,
        string Description,
        string? WeatherContext,
        DateTime? CreatedAt,
        DateTime? AcknowledgedAt);

    #endregion

    public static IEndpointRouteBuilder MapSensorEndpoints(this IEndpointRouteBuilder routes)
    {
        MapThresholds(routes.MapGroup("/api/sensor-thresholds").WithTags("Sensor thresholds").RequireAuthorization());
        MapTelemetry(routes);
        return routes;
    }

    #region Thresholds (RF04)

    private static void MapThresholds(RouteGroupBuilder thresholds)
    {
        thresholds.MapGet("/", async Task<Ok<IReadOnlyList<ThresholdResponse>>> (ThresholdsService.ThresholdsServiceClient client, CancellationToken ct) =>
            {
                var response = await client.ListThresholdsAsync(new Empty(), cancellationToken: ct);
                return TypedResults.Ok<IReadOnlyList<ThresholdResponse>>(response.Thresholds.Select(ToResponse).ToList());
            })
            .WithSummary("The global alert thresholds (administrator or manager)");

        thresholds.MapPut("/{type}", async Task<Ok<ThresholdResponse>> (SensorType type, UpdateThresholdBody body, ThresholdsService.ThresholdsServiceClient client, CancellationToken ct) =>
            {
                var request = new UpdateThresholdRequest { Type = ToProto(type), MinValue = body.MinValue, MaxValue = body.MaxValue };
                if (body.ExpectedVersion is not null) request.ExpectedVersion = body.ExpectedVersion.Value;
                return TypedResults.Ok(ToResponse(await client.UpdateThresholdAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Change a global alert threshold (administrator, RF04)");
    }

    #endregion

    #region Readings and alerts (RF17, RF18)

    private static void MapTelemetry(IEndpointRouteBuilder routes)
    {
        var farm = routes.MapGroup("/api/farms/{farmId}").WithTags("Sensors").RequireAuthorization();

        farm.MapGet("/readings", async Task<Ok<PagedResponse<ReadingResponse>>> (
                string farmId, SensorType? type, DateTime? from, DateTime? to, int? page, int? pageSize,
                SensorReadingsService.SensorReadingsServiceClient client, CancellationToken ct) =>
            {
                var request = new ListReadingsRequest { FarmId = farmId, From = from.ToTimestamp(), To = to.ToTimestamp(), Page = page ?? 1, PageSize = pageSize ?? 0 };
                if (type is not null) request.Type = ToProto(type.Value);

                var response = await client.ListReadingsAsync(request, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<ReadingResponse>(
                    response.Readings.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize));
            })
            .WithSummary("Your farm's reading history (manager, RF18)");

        farm.MapGet("/readings/latest", async Task<Ok<IReadOnlyList<ReadingResponse>>> (string farmId, SensorReadingsService.SensorReadingsServiceClient client, CancellationToken ct) =>
            {
                var response = await client.GetLatestReadingsAsync(new GetLatestReadingsRequest { FarmId = farmId }, cancellationToken: ct);
                return TypedResults.Ok<IReadOnlyList<ReadingResponse>>(response.Readings.Select(ToResponse).ToList());
            })
            .WithSummary("The latest reading of each sensor type on your farm (manager, RF18)");

        farm.MapGet("/alerts", async Task<Ok<PagedResponse<AlertResponse>>> (
                string farmId, bool? onlyOpen, DateTime? from, DateTime? to, int? page, int? pageSize,
                AlertsService.AlertsServiceClient client, CancellationToken ct) =>
            {
                var request = new ListAlertsRequest { FarmId = farmId, OnlyOpen = onlyOpen ?? false, From = from.ToTimestamp(), To = to.ToTimestamp(), Page = page ?? 1, PageSize = pageSize ?? 0 };
                var response = await client.ListAlertsAsync(request, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<AlertResponse>(
                    response.Alerts.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize));
            })
            .WithSummary("Your farm's sensor alerts (manager, RF17, RF18)");

        routes.MapPost("/api/alerts/{id}/acknowledge", async Task<Ok<AlertResponse>> (string id, AlertsService.AlertsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.AcknowledgeAlertAsync(new AcknowledgeAlertRequest { Id = id }, cancellationToken: ct))))
            .WithTags("Sensors")
            .RequireAuthorization()
            .WithSummary("Mark an alert as handled (manager)");
    }

    #endregion

    #region Mapping

    private static ThresholdResponse ToResponse(ThresholdInfo threshold) =>
        new(FromProto(threshold.Type), threshold.MinValue, threshold.MaxValue, threshold.Unit, threshold.UpdatedAt.AsDateTime(), threshold.Version);

    private static ReadingResponse ToResponse(ReadingInfo reading) =>
        new(reading.Id, reading.FarmId, reading.SensorId, FromProto(reading.Type), reading.Value, reading.Unit, reading.Timestamp.AsDateTime(), reading.ReceivedAt.AsDateTime());

    private static AlertResponse ToResponse(AlertInfo alert) => new(
        alert.Id,
        alert.FarmId,
        FromProto(alert.Type),
        alert.Value,
        alert.MinValue,
        alert.MaxValue,
        alert.Unit,
        alert.Description,
        alert.HasWeatherContext ? alert.WeatherContext : null,
        alert.CreatedAt.AsDateTime(),
        alert.AcknowledgedAt.AsDateTime());

    private static ProtoSensorType ToProto(SensorType type) => type switch
    {
        SensorType.Humidity => ProtoSensorType.Humidity,
        SensorType.Temperature => ProtoSensorType.Temperature,
        _ => ProtoSensorType.Unspecified
    };

    private static SensorType? FromProto(ProtoSensorType type) => type switch
    {
        ProtoSensorType.Humidity => SensorType.Humidity,
        ProtoSensorType.Temperature => SensorType.Temperature,
        _ => null
    };

    #endregion
}
