using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensors.Service.Models;

namespace Sensors.Service.Data.Configurations;

public class AlertThresholdConfiguration : IEntityTypeConfiguration<AlertThreshold>
{
    private static readonly DateTime SeededAt = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AlertThreshold> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Version).IsConcurrencyToken();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(FieldLimits.SensorTypeMaxLength)
            .IsRequired();

        builder.HasIndex(t => t.Type).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("CK_AlertThresholds_Min_Below_Max", "\"MinValue\" < \"MaxValue\""));

        // One global threshold per sensor type (RF04); the administrator only updates them.
        builder.HasData(
            new
            {
                Id = new Guid("5c0d8f1e-4f3b-4f5e-9a51-1d6f7e0a0001"),
                Type = SensorType.Humidity,
                MinValue = 30.0,
                MaxValue = 80.0,
                IsDeleted = false,
                Version = 1,
                CreatedAt = SeededAt
            },
            new
            {
                Id = new Guid("5c0d8f1e-4f3b-4f5e-9a51-1d6f7e0a0002"),
                Type = SensorType.Temperature,
                MinValue = 5.0,
                MaxValue = 35.0,
                IsDeleted = false,
                Version = 1,
                CreatedAt = SeededAt
            });
    }
}
