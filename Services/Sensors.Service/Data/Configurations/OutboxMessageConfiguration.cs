using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sensors.Service.Models;

namespace Sensors.Service.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType).HasMaxLength(FieldLimits.EventTypeMaxLength).IsRequired();
        builder.Property(m => m.RoutingKey).HasMaxLength(FieldLimits.RoutingKeyMaxLength).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(FieldLimits.OutboxErrorMaxLength);

        // The publisher only ever scans the pending messages, oldest first.
        builder.HasIndex(m => m.OccurredAt).HasFilter("\"PublishedAt\" IS NULL");
    }
}
