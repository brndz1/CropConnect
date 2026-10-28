using System.Text.Json;
using System.Text.Json.Serialization;
using Sensors.Service.Application;
using Sensors.Service.Models;

namespace Sensors.Service.Messaging;

// Contract of the JSON body Node-RED publishes to the sensors.readings exchange, e.g.
// { "messageId": "…", "farmId": "…", "sensorId": "s-01", "type": "humidity", "value": 27.4, "timestamp": "2026-10-28T10:00:00Z" }
public sealed record SensorReadingMessage(
    Guid MessageId,
    Guid FarmId,
    string? SensorId,
    SensorType? Type,
    double? Value,
    DateTimeOffset? Timestamp)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static SensorReadingMessage Parse(ReadOnlySpan<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<SensorReadingMessage>(body, SerializerOptions)
                   ?? throw new InvalidInputException("The reading message is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidInputException($"The reading message is not valid JSON: {exception.Message}");
        }
    }

    public RecordReadingCommand ToCommand() => new(
        MessageId,
        FarmId,
        SensorId ?? string.Empty,
        Type ?? throw new InvalidInputException("Sensor type is required."),
        Value ?? throw new InvalidInputException("Value is required."),
        Timestamp?.UtcDateTime ?? throw new InvalidInputException("Timestamp is required."));
}
