using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farms.Service.Data.Configurations;

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_StockReservations_Quantity_Positive", "\"Quantity\" > 0"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(FieldLimits.StatusMaxLength);

        builder.Property(r => r.UnitPrice).HasPrecision(FieldLimits.MoneyPrecision, FieldLimits.MoneyScale);

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.OrderId, r.ProductId }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
    }
}
