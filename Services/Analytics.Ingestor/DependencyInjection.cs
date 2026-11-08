using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Analytics.Ingestor.Application;
using Analytics.Ingestor.Catalog;
using Analytics.Ingestor.Messaging;
using Analytics.Ingestor.Storage;
using Farms.Service.Protos;

namespace Analytics.Ingestor;

public static class DependencyInjection
{
    public static IServiceCollection AddIngestorStorage(this IServiceCollection services)
    {
        services.AddOptions<MongoOptions>()
            .BindConfiguration(MongoOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(MongoOptions.ConnectionStringName) ?? options.ConnectionString)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PseudonymizationOptions>()
            .BindConfiguration(PseudonymizationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MongoRawStore>();
        services.AddSingleton<IRawStore>(provider => provider.GetRequiredService<MongoRawStore>());
        services.AddSingleton<PersonalDataScrubber>();
        services.AddSingleton<RawMessageHandler>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

        return services;
    }

    public static IServiceCollection AddIngestorMessaging(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddHostedService<RawMessageConsumer>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }

    public static IServiceCollection AddIngestorCatalogSnapshots(this IServiceCollection services)
    {
        services.AddOptions<CatalogSnapshotOptions>()
            .BindConfiguration(CatalogSnapshotOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddGrpcClient<FarmsService.FarmsServiceClient>((provider, options) =>
            options.Address = new Uri(provider.GetRequiredService<IOptions<CatalogSnapshotOptions>>().Value.FarmsServiceUrl));
        services.AddGrpcClient<ProductsService.ProductsServiceClient>((provider, options) =>
            options.Address = new Uri(provider.GetRequiredService<IOptions<CatalogSnapshotOptions>>().Value.FarmsServiceUrl));

        services.AddScoped<ICatalogReader, GrpcCatalogReader>();
        services.AddScoped<CatalogSnapshotService>();
        services.AddHostedService<CatalogSnapshotWorker>();

        return services;
    }
}
