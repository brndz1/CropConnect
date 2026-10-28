using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Farms.Service.Protos;
using Sensors.Service.Application;
using Sensors.Service.Application.Clients;
using Sensors.Service.Application.Events;
using Sensors.Service.Data;
using Sensors.Service.Messaging;
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

    public static IServiceCollection AddSensorsMessaging(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddHostedService<SensorReadingConsumer>();

        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<OutboxPublisher>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }

    public static IServiceCollection AddSensorsApplication(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddOptions<FarmDirectoryClientOptions>()
            .BindConfiguration(FarmDirectoryClientOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddGrpcClient<FarmDirectoryService.FarmDirectoryServiceClient>((provider, options) =>
        {
            var farmOptions = provider.GetRequiredService<IOptions<FarmDirectoryClientOptions>>().Value;
            options.Address = new Uri(farmOptions.Url);
        });

        services.AddScoped<IFarmDirectoryClient, FarmDirectoryClient>();

        services.AddOptions<SensorReadingOptions>()
            .BindConfiguration(SensorReadingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IThresholdApplicationService, ThresholdApplicationService>();
        services.AddScoped<IReadingIngestionService, ReadingIngestionService>();
        services.AddScoped<ITelemetryQueryService, TelemetryQueryService>();
        services.AddScoped<IAlertEventPublisher, OutboxAlertEventPublisher>();

        return services;
    }
}
