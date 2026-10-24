using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Service.Models;

namespace Orders.Service.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating)
            .IsRequired();

        builder.Property(r => r.Comment)
            .HasMaxLength(FieldLimits.MaxCommentLength);

        builder.HasIndex(r => r.OrderId)
            .IsUnique();

        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.FarmId);
        builder.HasIndex(r => r.CreatedAt);
    }
}
