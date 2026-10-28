using Microsoft.EntityFrameworkCore;
using Sensors.Service.Models;

namespace Sensors.Service.Data;

public class SensorsDbContext : DbContext
{
    public SensorsDbContext(DbContextOptions<SensorsDbContext> options) : base(options)
    {
    }

    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    public DbSet<AlertThreshold> AlertThresholds => Set<AlertThreshold>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SensorsDbContext).Assembly);
    }
}
