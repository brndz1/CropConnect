using System.Text;
using System.Text.Json.Serialization;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using AuthGrpc = Auth.Service.Protos;
using FarmsGrpc = Farms.Service.Protos;
using NotificationsGrpc = Notifications.Service.Protos;
using OrdersGrpc = Orders.Service.Protos;
using SensorsGrpc = Sensors.Service.Protos;
using UsersGrpc = Users.Service.Protos;

namespace CropConnect.ApiGateway.Infrastructure;

public static class GatewayServiceCollectionExtensions
{
    #region REST API

    public static IServiceCollection AddGatewayApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails();
        services.AddExceptionHandler<RpcExceptionHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        return services;
    }

    #endregion

    #region Security

    // The gateway rejects missing or invalid tokens early; each service still authorizes the call.
    public static IServiceCollection AddGatewaySecurity(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectDeactivatedUsersAsync };
            });

        services.AddMemoryCache();
        services.AddTransient<ActiveUserCheck>();
        services.AddAuthorization();

        return services;
    }

    private static async Task RejectDeactivatedUsersAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var token = (context.SecurityToken as JsonWebToken)?.EncodedToken;
        var check = context.HttpContext.RequestServices.GetRequiredService<ActiveUserCheck>();

        if (userId is null || token is null || !await check.IsActiveAsync(userId, token, context.HttpContext.RequestAborted))
        {
            context.Fail("The account is deactivated.");
        }
    }

    #endregion

    #region Downstream services

    public static IServiceCollection AddDownstreamServices(this IServiceCollection services)
    {
        services.AddOptions<DownstreamOptions>()
            .BindConfiguration(DownstreamOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddTransient<ForwardAuthorizationHandler>();
        services.AddSingleton<DeadlineInterceptor>();

        services.AddDownstreamClient<AuthGrpc.AuthService.AuthServiceClient>(o => o.Auth);
        services.AddDownstreamClient<AuthGrpc.UserAccountsService.UserAccountsServiceClient>(o => o.Auth);
        services.AddDownstreamClient<UsersGrpc.UsersService.UsersServiceClient>(o => o.Users);
        services.AddDownstreamClient<FarmsGrpc.FarmsService.FarmsServiceClient>(o => o.Farms);
        services.AddDownstreamClient<FarmsGrpc.PickupSchedulesService.PickupSchedulesServiceClient>(o => o.Farms);
        services.AddDownstreamClient<FarmsGrpc.ProductsService.ProductsServiceClient>(o => o.Farms);
        services.AddDownstreamClient<OrdersGrpc.OrdersService.OrdersServiceClient>(o => o.Orders);
        services.AddDownstreamClient<OrdersGrpc.ReviewsService.ReviewsServiceClient>(o => o.Orders);
        services.AddDownstreamClient<SensorsGrpc.ThresholdsService.ThresholdsServiceClient>(o => o.Sensors);
        services.AddDownstreamClient<SensorsGrpc.SensorReadingsService.SensorReadingsServiceClient>(o => o.Sensors);
        services.AddDownstreamClient<SensorsGrpc.AlertsService.AlertsServiceClient>(o => o.Sensors);
        services.AddDownstreamClient<NotificationsGrpc.NotificationsService.NotificationsServiceClient>(o => o.Notifications);

        return services;
    }

    private static void AddDownstreamClient<TClient>(this IServiceCollection services, Func<DownstreamOptions, string> address)
        where TClient : class
    {
        services.AddGrpcClient<TClient>((provider, options) =>
            {
                var downstream = provider.GetRequiredService<IOptions<DownstreamOptions>>().Value;
                options.Address = new Uri(address(downstream));
            })
            .AddInterceptor<DeadlineInterceptor>(InterceptorScope.Client)
            .AddHttpMessageHandler<ForwardAuthorizationHandler>();
    }

    #endregion
}
