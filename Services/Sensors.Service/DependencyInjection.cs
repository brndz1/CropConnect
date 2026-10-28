using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sensors.Service.Data;
using Sensors.Service.Services;

namespace Sensors.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddSensorsApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.Interceptors.Add<ExceptionMappingInterceptor>();
        });

        return services;
    }

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
