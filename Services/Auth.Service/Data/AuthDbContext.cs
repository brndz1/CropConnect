using Auth.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Auth.Service.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map Role enum to a string in the database (for readability)
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();
            
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Username).HasMaxLength(FieldLimits.UsernameMaxLength);
            entity.Property(u => u.Email).HasMaxLength(FieldLimits.EmailMaxLength);
            entity.Property(u => u.Role).HasMaxLength(FieldLimits.RoleMaxLength);
        });

        // Ensure email and username are unique
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
            
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<AccountToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Purpose).HasConversion<string>().HasMaxLength(FieldLimits.TokenPurposeMaxLength);
            entity.Property(t => t.TokenHash).HasMaxLength(FieldLimits.TokenHashLength).IsFixedLength().IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => new { t.UserId, t.Purpose });
            entity.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(FieldLimits.TokenHashLength).IsFixedLength().IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.FamilyId);
            entity.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.EventType).HasMaxLength(FieldLimits.EventTypeMaxLength).IsRequired();
            entity.Property(m => m.RoutingKey).HasMaxLength(FieldLimits.RoutingKeyMaxLength).IsRequired();
            entity.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(m => m.LastError).HasMaxLength(FieldLimits.OutboxErrorMaxLength);
            entity.Property(m => m.TraceParent).HasMaxLength(FieldLimits.TraceParentMaxLength);

            // The publisher only ever scans the pending messages, oldest first.
            entity.HasIndex(m => m.OccurredAt).HasFilter("\"PublishedAt\" IS NULL");
        });
    }
}
