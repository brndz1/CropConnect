using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Service.Models;

namespace Notifications.Service.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Ignore(n => n.IsRead);

        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(FieldLimits.TitleMaxLength).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(FieldLimits.MessageMaxLength).IsRequired();
        builder.Property(n => n.EmailStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.EmailLastError).HasMaxLength(FieldLimits.EmailErrorMaxLength);

        // A redelivered event must not notify the same person twice.
        builder.HasIndex(n => new { n.SourceEventId, n.RecipientId }).IsUnique();

        // A user's inbox, newest first.
        builder.HasIndex(n => new { n.RecipientId, n.CreatedAt }).IsDescending(false, true);

        // The email dispatcher only scans the emails still to send.
        builder.HasIndex(n => n.EmailNextAttemptAt).HasFilter("\"EmailStatus\" = 'Pending'");
    }
}
