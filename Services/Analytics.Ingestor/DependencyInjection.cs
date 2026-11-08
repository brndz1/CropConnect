using Microsoft.Extensions.DependencyInjection.Extensions;
using Analytics.Ingestor.Application;
using Analytics.Ingestor.Messaging;
using Analytics.Ingestor.Storage;

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
}
