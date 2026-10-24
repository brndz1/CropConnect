using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Service.Models;

namespace Orders.Service.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.Property(o => o.TotalAmount)
            .HasPrecision(FieldLimits.MoneyPrecision, FieldLimits.MoneyScale)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasMaxLength(FieldLimits.MaxNotesLength);

        builder.Property(o => o.CancellationReason)
            .HasMaxLength(FieldLimits.MaxReasonLength);

        builder.Property(o => o.Status)
            .IsRequired();

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.FarmId);
        builder.HasIndex(o => o.FarmOwnerId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Review)
            .WithOne(r => r.Order)
            .HasForeignKey<Review>(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
