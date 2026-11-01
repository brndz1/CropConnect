using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Service.Models;

namespace Notifications.Service.Data.Configurations;

public class AccountEmailConfiguration : IEntityTypeConfiguration<AccountEmail>
{
    public void Configure(EntityTypeBuilder<AccountEmail> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.HasLink);

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.ToAddress).HasMaxLength(FieldLimits.EmailAddressMaxLength).IsRequired();
        builder.Property(e => e.ToName).HasMaxLength(FieldLimits.EmailNameMaxLength).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(FieldLimits.TitleMaxLength).IsRequired();
        builder.Property(e => e.Text).HasMaxLength(FieldLimits.AccountEmailTextMaxLength).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(FieldLimits.EmailErrorMaxLength);
        builder.Property(e => e.TraceParent).HasMaxLength(FieldLimits.TraceParentMaxLength);

        // A redelivered event must not send the same email twice.
        builder.HasIndex(e => e.SourceEventId).IsUnique();

        // The email dispatcher only scans the emails still to send.
        builder.HasIndex(e => e.NextAttemptAt).HasFilter("\"Status\" = 'Pending'");
    }
}
