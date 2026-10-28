using Google.Protobuf.WellKnownTypes;
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

    #region Time

    public static Timestamp ToTimestamp(this DateTime value) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    #endregion
}
