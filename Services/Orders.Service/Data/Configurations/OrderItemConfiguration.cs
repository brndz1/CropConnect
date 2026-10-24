using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Service.Models;

namespace Orders.Service.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName)
            .HasMaxLength(FieldLimits.MaxNameLength)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasPrecision(FieldLimits.MoneyPrecision, FieldLimits.MoneyScale)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Ignore(i => i.TotalPrice);

        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.ProductId);
    }
}
