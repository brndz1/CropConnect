using Auth.Service.Data;
using Auth.Service.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Auth.Service.Tests.Infrastructure;

// An in-memory SQLite database with the service's real model, kept open for the lifetime of the test.
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AuthDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

public static class TestJwt
{
    public const string Secret = "UnitTestSecretKeyThatIsLongEnough-0123456789";

    public static IConfiguration Settings(string secret = Secret, int expirationMinutes = 60) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = secret,
                ["JwtSettings:Issuer"] = "CropConnect.Auth",
                ["JwtSettings:Audience"] = "CropConnect.Clients",
                ["JwtSettings:ExpirationMinutes"] = expirationMinutes.ToString()
            })
            .Build();

    public static JwtTokenGenerator Generator(string secret = Secret, int expirationMinutes = 60) => new(Settings(secret, expirationMinutes));

    public static JwtTokenValidator Validator(string secret = Secret) => new(Settings(secret));
}
