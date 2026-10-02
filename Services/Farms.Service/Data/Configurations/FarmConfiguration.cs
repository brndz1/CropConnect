using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farms.Service.Data.Configurations;

public class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Version).IsConcurrencyToken();

        builder.Property(f => f.Name).HasMaxLength(FieldLimits.NameMaxLength).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(FieldLimits.DescriptionMaxLength).IsRequired();
        builder.Property(f => f.Location).HasMaxLength(FieldLimits.LocationMaxLength).IsRequired();

        builder.HasIndex(f => f.OwnerId);

        builder.HasIndex(f => f.Name);
    }
}
