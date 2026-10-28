using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Sensors.Service.Data;

public static class DatabaseStartupExtensions
{
    public static async Task ApplyMigrationsIfConfiguredAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!options.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SensorsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
