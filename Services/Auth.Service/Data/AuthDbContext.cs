using Auth.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Auth.Service.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map Role enum to a string in the database (for readability)
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();
            
        // Ensure email and username are unique
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
            
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}
