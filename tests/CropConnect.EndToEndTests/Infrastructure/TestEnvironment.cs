namespace CropConnect.EndToEndTests.Infrastructure;

// Addresses of the running platform (Docker Compose by default). Override them with environment variables
// to test another environment, e.g. the Aspire AppHost, whose container ports change on every run.
public static class TestEnvironment
{
    public static Uri Gateway { get; } = Read("CROPCONNECT_GATEWAY_URL", "http://localhost:5000/");

    public static Uri RabbitMqManagement { get; } = Read("CROPCONNECT_RABBITMQ_MANAGEMENT_URL", "http://localhost:15673/");

    public static Uri Mailpit { get; } = Read("CROPCONNECT_MAILPIT_URL", "http://localhost:8025/");

    public static string RabbitMqUser { get; } = Environment.GetEnvironmentVariable("CROPCONNECT_RABBITMQ_USER") ?? "guest";

    public static string RabbitMqPassword { get; } = Environment.GetEnvironmentVariable("CROPCONNECT_RABBITMQ_PASSWORD") ?? "guest";

    public static string AdminEmail { get; } = Environment.GetEnvironmentVariable("CROPCONNECT_ADMIN_EMAIL") ?? "admin@cropconnect.test";

    public static string AdminPassword { get; } = Environment.GetEnvironmentVariable("CROPCONNECT_ADMIN_PASSWORD") ?? "DevAdmin-ChangeMe-123";

    private static Uri Read(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name) ?? fallback;
        return new Uri(value.EndsWith('/') ? value : value + "/");
    }
}
