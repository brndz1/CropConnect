using System.Text;
using Sensors.Service.Messaging;
using Sensors.Service.Models;

namespace Sensors.Service.Tests.Models;

public sealed class ReadingAndAlertTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    private static SensorReading Reading(SensorType type, double value, DateTime? timestamp = null) =>
        SensorReading.Create(Guid.NewGuid(), Guid.NewGuid(), "s-01", type, value, timestamp ?? Now, Now, Skew);

    #region Readings

    [Theory]
    [InlineData(SensorType.Humidity, -0.1)]
    [InlineData(SensorType.Humidity, 100.1)]
    [InlineData(SensorType.Temperature, -50.1)]
    [InlineData(SensorType.Temperature, 70.1)]
    [InlineData(SensorType.Temperature, double.NaN)]
    public void Physically_impossible_values_are_rejected(SensorType type, double value)
    {
        Assert.Throws<InvalidInputException>(() => Reading(type, value));
    }

    [Fact]
    public void Readings_from_the_future_beyond_the_clock_skew_are_rejected()
    {
        Reading(SensorType.Humidity, 50, Now + Skew);

        Assert.Throws<InvalidInputException>(() => Reading(SensorType.Humidity, 50, Now + Skew + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Reading_needs_a_message_farm_sensor_and_known_type()
    {
        Assert.Throws<InvalidInputException>(() => SensorReading.Create(Guid.Empty, Guid.NewGuid(), "s", SensorType.Humidity, 50, Now, Now, Skew));
        Assert.Throws<InvalidInputException>(() => SensorReading.Create(Guid.NewGuid(), Guid.Empty, "s", SensorType.Humidity, 50, Now, Now, Skew));
        Assert.Throws<InvalidInputException>(() => SensorReading.Create(Guid.NewGuid(), Guid.NewGuid(), " ", SensorType.Humidity, 50, Now, Now, Skew));
        Assert.Throws<InvalidInputException>(() => SensorReading.Create(Guid.NewGuid(), Guid.NewGuid(), "s", (SensorType)7, 50, Now, Now, Skew));
    }

    #endregion

    #region Thresholds and alerts

    [Fact]
    public void Threshold_bounds_are_inclusive()
    {
        var threshold = AlertThreshold.Create(SensorType.Humidity, 30, 80, Now);

        Assert.True(threshold.IsWithin(30));
        Assert.True(threshold.IsWithin(80));
        Assert.False(threshold.IsWithin(29.9));
        Assert.False(threshold.IsWithin(80.1));
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(60, 40)]
    [InlineData(-1, 40)]
    [InlineData(10, 101)]
    public void Threshold_range_must_be_ordered_and_physical(double min, double max)
    {
        Assert.Throws<InvalidInputException>(() => AlertThreshold.Create(SensorType.Humidity, min, max, Now));
    }

    [Fact]
    public void Low_reading_alert_describes_the_breached_minimum()
    {
        var threshold = AlertThreshold.Create(SensorType.Humidity, 30, 80, Now);
        var alert = Alert.Create(Reading(SensorType.Humidity, 18.4), threshold, Guid.NewGuid(), Now);

        Assert.Equal("Humidity of 18.4% from sensor s-01 is below the minimum of 30%.", alert.Description);
        Assert.Equal(30, alert.MinValue);
        Assert.True(alert.IsOpen);
    }

    [Fact]
    public void High_reading_alert_describes_the_breached_maximum()
    {
        var threshold = AlertThreshold.Create(SensorType.Temperature, 5, 35, Now);
        var alert = Alert.Create(Reading(SensorType.Temperature, 41), threshold, Guid.NewGuid(), Now);

        Assert.Equal("Temperature of 41°C from sensor s-01 is above the maximum of 35°C.", alert.Description);
    }

    [Fact]
    public void Alerts_are_only_for_out_of_range_readings_of_the_same_type()
    {
        var humidity = AlertThreshold.Create(SensorType.Humidity, 30, 80, Now);

        Assert.Throws<InvalidOperationException>(() => Alert.Create(Reading(SensorType.Humidity, 50), humidity, Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() => Alert.Create(Reading(SensorType.Temperature, 50), humidity, Guid.NewGuid(), Now));
    }

    [Fact]
    public void Alert_is_acknowledged_once()
    {
        var threshold = AlertThreshold.Create(SensorType.Humidity, 30, 80, Now);
        var alert = Alert.Create(Reading(SensorType.Humidity, 10), threshold, Guid.NewGuid(), Now);
        var manager = Guid.NewGuid();

        Assert.True(alert.Acknowledge(manager, Now));
        Assert.False(alert.Acknowledge(Guid.NewGuid(), Now.AddMinutes(1)));
        Assert.Equal(manager, alert.AcknowledgedBy);
        Assert.False(alert.IsOpen);
    }

    #endregion

    #region Message contract

    [Fact]
    public void Node_red_message_parses_into_a_command()
    {
        var farmId = Guid.NewGuid();
        var body = Encoding.UTF8.GetBytes(
            $$"""{"messageId":"{{Guid.NewGuid()}}","farmId":"{{farmId}}","sensorId":"sim-1","type":"humidity","value":27.4,"timestamp":"2026-05-01T10:00:00+01:00"}""");

        var command = SensorReadingMessage.Parse(body).ToCommand();

        Assert.Equal(farmId, command.FarmId);
        Assert.Equal(SensorType.Humidity, command.Type);
        Assert.Equal(27.4, command.Value);
        Assert.Equal(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc), command.Timestamp);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"type":"pressure"}""")]
    [InlineData("""{"type":1}""")]
    [InlineData("null")]
    public void Malformed_messages_are_invalid_input(string json)
    {
        Assert.Throws<InvalidInputException>(() => SensorReadingMessage.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Messages_missing_a_field_are_invalid_input()
    {
        var body = Encoding.UTF8.GetBytes($$"""{"messageId":"{{Guid.NewGuid()}}","farmId":"{{Guid.NewGuid()}}","type":"humidity","value":27.4}""");

        Assert.Throws<InvalidInputException>(() => SensorReadingMessage.Parse(body).ToCommand());
    }

    #endregion
}
