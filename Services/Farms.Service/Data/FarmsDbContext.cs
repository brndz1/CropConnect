using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Farms.Service.Data;

public class FarmsDbContext : DbContext
{
    public FarmsDbContext(DbContextOptions<FarmsDbContext> options) : base(options)
    {
    }

    public DbSet<Farm> Farms => Set<Farm>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarmsDbContext).Assembly);
    }
}
