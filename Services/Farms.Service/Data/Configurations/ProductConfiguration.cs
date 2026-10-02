using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farms.Service.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_StockQuantity_NonNegative", "\"StockQuantity\" >= 0");
            t.HasCheckConstraint("CK_Products_Price_NonNegative", "\"Price\" >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Version).IsConcurrencyToken();

        builder.Property(p => p.Name).HasMaxLength(FieldLimits.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(FieldLimits.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.Unit).HasMaxLength(FieldLimits.UnitMaxLength).IsRequired();
        builder.Property(p => p.Price).HasPrecision(FieldLimits.MoneyPrecision, FieldLimits.MoneyScale);

        builder.HasOne(p => p.Farm)
            .WithMany()
            .HasForeignKey(p => p.FarmId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.FarmId);
        builder.HasIndex(p => p.Name);
    }
}
