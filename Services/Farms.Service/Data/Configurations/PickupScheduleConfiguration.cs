using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farms.Service.Data.Configurations;

public class PickupScheduleConfiguration : IEntityTypeConfiguration<PickupSchedule>
{
    public void Configure(EntityTypeBuilder<PickupSchedule> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_PickupSchedules_CloseTime_After_OpenTime", "\"CloseTime\" > \"OpenTime\""));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Version).IsConcurrencyToken();

        builder.Property(s => s.DayOfWeek)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.OpenTime)
            .IsRequired();

        builder.Property(s => s.CloseTime)
            .IsRequired();

        builder.HasOne(s => s.Farm)
            .WithMany(f => f.PickupSchedules)
            .HasForeignKey(s => s.FarmId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.FarmId);
        builder.HasIndex(s => new { s.FarmId, s.DayOfWeek });
    }
}
