using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orders.Service.Application;
using Orders.Service.Application.Clients;
using Orders.Service.Application.Events;
using Orders.Service.Data;
using Orders.Service.Services;
using Farms.Service.Protos;

namespace Orders.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.Interceptors.Add<ExceptionMappingInterceptor>();
        });

        return services;
    }

    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<OrdersDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString);

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddHealthChecks().AddCheck<OrdersDbHealthCheck>("orders-db");
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        services.AddOptions<OrderOptions>()
            .BindConfiguration(OrderOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FarmStockClientOptions>()
            .BindConfiguration(FarmStockClientOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddGrpcClient<StockService.StockServiceClient>((provider, options) =>
        {
            var farmOptions = provider.GetRequiredService<IOptions<FarmStockClientOptions>>().Value;
            options.Address = new Uri(farmOptions.Url);
        });

        services.AddScoped<IFarmStockClient, FarmStockClient>();
        services.AddScoped<IOrderEventPublisher, LoggingOrderEventPublisher>();
        services.AddScoped<IOrderApplicationService, OrderApplicationService>();
        services.AddScoped<IReviewApplicationService, ReviewApplicationService>();
        services.AddHostedService<StockReleaseWorker>();

        return services;
    }
}
