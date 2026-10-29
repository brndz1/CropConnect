using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Auth.Service.Protos;
using Farms.Service.Protos;
using Notifications.Service.Application;
using Notifications.Service.Application.Clients;
using Notifications.Service.Application.Email;
using Notifications.Service.Data;
using Notifications.Service.Messaging;
using Notifications.Service.Services;

namespace Notifications.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.Interceptors.Add<ExceptionMappingInterceptor>();
        });

        return services;
    }

    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<NotificationsDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString);

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddHealthChecks().AddCheck<NotificationsDbHealthCheck>("notifications-db");
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    public static IServiceCollection AddNotificationsMessaging(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddHostedService<NotificationEventConsumer>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }

    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
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

        services.AddOptions<UserDirectoryClientOptions>()
            .BindConfiguration(UserDirectoryClientOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddGrpcClient<UserDirectoryService.UserDirectoryServiceClient>((provider, options) =>
        {
            var authOptions = provider.GetRequiredService<IOptions<UserDirectoryClientOptions>>().Value;
            options.Address = new Uri(authOptions.Url);
        });

        services.AddScoped<IUserDirectoryClient, UserDirectoryClient>();

        services.AddScoped<INotificationIngestionService, NotificationIngestionService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();

        return services;
    }

    public static IServiceCollection AddNotificationsEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                email => email.Provider != EmailProvider.Mailtrap || !string.IsNullOrWhiteSpace(email.Mailtrap.ApiToken),
                "Email:Mailtrap:ApiToken is required when the email provider is Mailtrap.")
            .ValidateOnStart();

        // The provider is chosen once at startup; both senders are typed HTTP clients.
        var provider = configuration.GetSection(EmailOptions.SectionName).GetValue(nameof(EmailOptions.Provider), EmailProvider.Mailpit);

        if (provider == EmailProvider.Mailtrap)
        {
            services.AddHttpClient<IEmailSender, MailtrapEmailSender>((sp, client) =>
            {
                var email = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
                client.BaseAddress = new Uri(email.Mailtrap.SandboxInboxId is null
                    ? MailtrapEmailSender.SendingBaseUrl
                    : MailtrapEmailSender.SandboxBaseUrl);
                client.Timeout = email.Timeout;
            });
        }
        else
        {
            services.AddHttpClient<IEmailSender, MailpitEmailSender>((sp, client) =>
            {
                var email = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
                client.BaseAddress = new Uri(email.Mailpit.BaseUrl);
                client.Timeout = email.Timeout;
            });
        }

        services.AddScoped<IEmailDispatchService, EmailDispatchService>();
        services.AddHostedService<EmailDispatchWorker>();

        return services;
    }
}
