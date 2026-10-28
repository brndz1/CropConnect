using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensors.Service.Models;

namespace Sensors.Service.Data.Configurations;

public class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.SensorId)
            .HasMaxLength(FieldLimits.SensorIdMaxLength)
            .IsRequired();

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasMaxLength(FieldLimits.SensorTypeMaxLength)
            .IsRequired();

        builder.HasIndex(r => r.MessageId).IsUnique();
        builder.HasIndex(r => new { r.FarmId, r.Type, r.Timestamp }).IsDescending(false, false, true);
    }
}
