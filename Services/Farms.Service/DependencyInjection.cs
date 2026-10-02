using Farms.Service.Services;
using Farms.Service.Application;
using Farms.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Farms.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddFarmsApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.Interceptors.Add<ExceptionMappingInterceptor>();
        });

        return services;
    }

    public static IServiceCollection AddFarmsInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<FarmsDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString);

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddHealthChecks().AddCheck<FarmsDbHealthCheck>("farms-db");
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    public static IServiceCollection AddFarmsApplication(this IServiceCollection services)
    {
        services.AddOptions<FarmOptions>()
            .BindConfiguration(FarmOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StockReservationOptions>()
            .BindConfiguration(StockReservationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IFarmApplicationService, FarmApplicationService>();
        services.AddScoped<IProductApplicationService, ProductApplicationService>();
        services.AddScoped<IStockApplicationService, StockApplicationService>();
        services.AddHostedService<ReservationExpiryWorker>();

        return services;
    }
}
