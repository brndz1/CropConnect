using Google.Protobuf.WellKnownTypes;
using Sensors.Service.Application;
using Sensors.Service.Models;
using Sensors.Service.Protos;
using SensorTypeProto = Sensors.Service.Protos.SensorType;

namespace Sensors.Service.Services;

public static class ProtoMappings
{
    #region Sensor type

    public static SensorTypeProto ToProto(this Models.SensorType type) => type switch
    {
        Models.SensorType.Humidity => SensorTypeProto.Humidity,
        Models.SensorType.Temperature => SensorTypeProto.Temperature,
        _ => SensorTypeProto.Unspecified
    };

    public static Models.SensorType ToDomain(this SensorTypeProto type) => type switch
    {
        SensorTypeProto.Humidity => Models.SensorType.Humidity,
        SensorTypeProto.Temperature => Models.SensorType.Temperature,
        _ => throw new InvalidInputException("Sensor type is required.")
    };

    #endregion

    #region Thresholds

    public static ThresholdInfo ToProto(this AlertThreshold threshold) => new()
    {
        Type = threshold.Type.ToProto(),
        MinValue = threshold.MinValue,
        MaxValue = threshold.MaxValue,
        Unit = threshold.Type.Unit(),
        UpdatedAt = (threshold.UpdatedAt ?? threshold.CreatedAt).ToTimestamp(),
        Version = threshold.Version
    };

    public static ListThresholdsResponse ToProto(this IEnumerable<AlertThreshold> thresholds)
    {
        var response = new ListThresholdsResponse();
        response.Thresholds.AddRange(thresholds.Select(t => t.ToProto()));
        return response;
    }

    #endregion

    #region Readings

    public static ReadingInfo ToProto(this SensorReading reading) => new()
    {
        Id = reading.Id.ToString(),
        FarmId = reading.FarmId.ToString(),
        SensorId = reading.SensorId,
        Type = reading.Type.ToProto(),
        Value = reading.Value,
        Unit = reading.Type.Unit(),
        Timestamp = reading.Timestamp.ToTimestamp(),
        ReceivedAt = reading.ReceivedAt.ToTimestamp()
    };

    public static ListReadingsResponse ToProto(this PagedResult<SensorReading> page)
    {
        var response = new ListReadingsResponse { TotalCount = page.TotalCount, Page = page.Page, PageSize = page.PageSize };
        response.Readings.AddRange(page.Items.Select(r => r.ToProto()));
        return response;
    }

    public static RecordReadingResponse ToProto(this RecordReadingResult result)
    {
        var response = new RecordReadingResponse
        {
            Reading = result.Reading.ToProto(),
            AlreadyProcessed = result.AlreadyProcessed
        };

        if (result.Alert is { } alert)
        {
            response.Alert = alert.ToProto();
        }

        return response;
    }

    #endregion

    #region Alerts

    public static AlertInfo ToProto(this Alert alert)
    {
        var info = new AlertInfo
        {
            Id = alert.Id.ToString(),
            FarmId = alert.FarmId.ToString(),
            Type = alert.Type.ToProto(),
            Value = alert.Value,
            MinValue = alert.MinValue,
            MaxValue = alert.MaxValue,
            Unit = alert.Type.Unit(),
            Description = alert.Description,
            CreatedAt = alert.CreatedAt.ToTimestamp()
        };

        if (alert.WeatherContext is { } weatherContext)
        {
            info.WeatherContext = weatherContext;
        }

        if (alert.AcknowledgedAt is { } acknowledgedAt)
        {
            info.AcknowledgedAt = acknowledgedAt.ToTimestamp();
        }

        return info;
    }

    public static ListAlertsResponse ToProto(this PagedResult<Alert> page)
    {
        var response = new ListAlertsResponse { TotalCount = page.TotalCount, Page = page.Page, PageSize = page.PageSize };
        response.Alerts.AddRange(page.Items.Select(a => a.ToProto()));
        return response;
    }

    #endregion

    #region Time

    public static Timestamp ToTimestamp(this DateTime value) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    #endregion
}
