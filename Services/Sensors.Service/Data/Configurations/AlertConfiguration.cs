using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensors.Service.Models;

namespace Sensors.Service.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(FieldLimits.SensorTypeMaxLength)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(FieldLimits.DescriptionMaxLength)
            .IsRequired();

        builder.Property(a => a.WeatherContext)
            .HasMaxLength(FieldLimits.WeatherContextMaxLength);

        builder.HasOne<SensorReading>()
            .WithMany()
            .HasForeignKey(a => a.ReadingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.FarmId, a.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(a => new { a.FarmId, a.Type, a.CreatedAt }).IsDescending(false, false, true);
    }
}
