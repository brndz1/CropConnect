using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sensors.Service.Data;

namespace Sensors.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddSensorsInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<SensorsDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString);

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddHealthChecks().AddCheck<SensorsDbHealthCheck>("sensors-db");
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
