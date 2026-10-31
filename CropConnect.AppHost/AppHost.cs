// Runs the whole platform for local development with one command (dotnet run --project CropConnect.AppHost):
// infrastructure in containers, every service as a project, and their logs, traces and metrics in the
// Aspire dashboard (RNF observability). Secrets not set here come from each service's appsettings.Development.json.

var builder = DistributedApplication.CreateBuilder(args);

#region Infrastructure

// Database per service (section 5.1): one PostgreSQL server, one database owned by each service.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("cropconnect-aspire-postgres");

var authDb = postgres.AddDatabase("AuthDb", "auth_db");
var usersDb = postgres.AddDatabase("UsersDb", "users_db");
var farmsDb = postgres.AddDatabase("FarmsDb", "farms_db");
var ordersDb = postgres.AddDatabase("OrdersDb", "orders_db");
var sensorsDb = postgres.AddDatabase("SensorsDb", "sensors_db");
var notificationsDb = postgres.AddDatabase("NotificationsDb", "notifications_db");

var rabbitUser = builder.AddParameter("rabbitmq-user", "guest");
var rabbitPassword = builder.AddParameter("rabbitmq-password", "guest", secret: true);
var rabbitmq = builder.AddRabbitMQ("rabbitmq", rabbitUser, rabbitPassword)
    .WithManagementPlugin()
    .WithDataVolume("cropconnect-aspire-rabbitmq");

// Catches the notification emails; its web inbox is linked from the dashboard.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
    .WithHttpEndpoint(targetPort: 8025, name: "http");

#endregion

#region Services

var auth = builder.AddProject<Projects.Auth_Service>("auth-service")
    .WithReference(authDb)
    .WaitFor(authDb);

var users = builder.AddProject<Projects.Users_Service>("users-service")
    .WithReference(usersDb)
    .WaitFor(usersDb);

var farms = builder.AddProject<Projects.Farms_Service>("farms-service")
    .WithReference(farmsDb)
    .WaitFor(farmsDb);

var orders = builder.AddProject<Projects.Orders_Service>("orders-service")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithRabbitMq(rabbitmq, rabbitUser, rabbitPassword)
    .WithEnvironment("FarmsService__Url", farms.GetEndpoint("http"));

var sensors = builder.AddProject<Projects.Sensors_Service>("sensors-service")
    .WithReference(sensorsDb)
    .WaitFor(sensorsDb)
    .WithRabbitMq(rabbitmq, rabbitUser, rabbitPassword)
    .WithEnvironment("FarmsService__Url", farms.GetEndpoint("http"));

var notifications = builder.AddProject<Projects.Notifications_Service>("notifications-service")
    .WithReference(notificationsDb)
    .WaitFor(notificationsDb)
    .WithRabbitMq(rabbitmq, rabbitUser, rabbitPassword)
    .WithEnvironment("FarmsService__Url", farms.GetEndpoint("http"))
    .WithEnvironment("AuthService__Url", auth.GetEndpoint("http"))
    .WithEnvironment("Email__Provider", "Mailpit")
    .WithEnvironment("Email__Mailpit__BaseUrl", mailpit.GetEndpoint("http"))
    .WaitFor(mailpit);

#endregion

#region Edge

// The only REST entry point (section 5.1); every downstream address comes from the resources above.
var gateway = builder.AddProject<Projects.CropConnect_ApiGateway>("apigateway")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Downstream__Auth", auth.GetEndpoint("http"))
    .WithEnvironment("Downstream__Users", users.GetEndpoint("http"))
    .WithEnvironment("Downstream__Farms", farms.GetEndpoint("http"))
    .WithEnvironment("Downstream__Orders", orders.GetEndpoint("http"))
    .WithEnvironment("Downstream__Sensors", sensors.GetEndpoint("http"))
    .WithEnvironment("Downstream__Notifications", notifications.GetEndpoint("http"))
    .WithHttpHealthCheck("/health")
    .WaitFor(auth)
    .WaitFor(users)
    .WaitFor(farms)
    .WaitFor(orders)
    .WaitFor(sensors)
    .WaitFor(notifications);

// Simulated farm sensors (RF16): Node-RED reads the active farms from the gateway and publishes their readings.
builder.AddDockerfile("sensor-simulator", "../Simulation/node-red")
    .WithHttpEndpoint(targetPort: 1880, name: "http")
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.GetEndpoint("tcp").Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.GetEndpoint("tcp").Property(EndpointProperty.TargetPort))
    .WithEnvironment("RABBITMQ_USER", rabbitUser)
    .WithEnvironment("RABBITMQ_PASSWORD", rabbitPassword)
    .WithEnvironment("GATEWAY_URL", gateway.GetEndpoint("http"))
    .WaitFor(rabbitmq)
    .WaitFor(gateway);

// The web app, served by nginx, which forwards /api to the gateway.
builder.AddDockerfile("web", "../web")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithExternalHttpEndpoints()
    .WithEnvironment("GATEWAY_URL", gateway.GetEndpoint("http"))
    .WithHttpHealthCheck("/healthz")
    .WaitFor(gateway);

#endregion

builder.Build().Run();

internal static class ResourceBuilderExtensions
{
    // The services read RabbitMq:* options rather than a connection string, so the broker's endpoint and
    // credentials are passed one by one.
    public static IResourceBuilder<ProjectResource> WithRabbitMq(
        this IResourceBuilder<ProjectResource> project,
        IResourceBuilder<RabbitMQServerResource> rabbitmq,
        IResourceBuilder<ParameterResource> userName,
        IResourceBuilder<ParameterResource> password)
    {
        var endpoint = rabbitmq.GetEndpoint("tcp");

        return project
            .WithEnvironment("RabbitMq__HostName", endpoint.Property(EndpointProperty.Host))
            .WithEnvironment("RabbitMq__Port", endpoint.Property(EndpointProperty.Port))
            .WithEnvironment("RabbitMq__UserName", userName)
            .WithEnvironment("RabbitMq__Password", password)
            .WaitFor(rabbitmq);
    }
}
