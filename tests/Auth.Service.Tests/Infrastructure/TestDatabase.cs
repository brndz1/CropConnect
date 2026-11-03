using System.Security.Cryptography;
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

// An RSA key pair made for the test run, like the one Auth.Service signs with (RS256).
public sealed record TestKeyPair(string PrivateKey, string PublicKey)
{
    public static TestKeyPair Create()
    {
        using var rsa = RSA.Create(2048);
        return new(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()), Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
    }
}

public static class TestJwt
{
    public static readonly TestKeyPair Keys = TestKeyPair.Create();

    public static IConfiguration Settings(TestKeyPair? keys = null, int expirationMinutes = 60) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:PrivateKey"] = (keys ?? Keys).PrivateKey,
                ["JwtSettings:PublicKey"] = (keys ?? Keys).PublicKey,
                ["JwtSettings:Issuer"] = "CropConnect.Auth",
                ["JwtSettings:Audience"] = "CropConnect.Clients",
                ["JwtSettings:ExpirationMinutes"] = expirationMinutes.ToString()
            })
            .Build();

    public static JwtTokenGenerator Generator(TestKeyPair? keys = null, int expirationMinutes = 60) => new(Settings(keys, expirationMinutes));

    public static JwtTokenValidator Validator(TestKeyPair? keys = null) => new(Settings(keys));
}

// A clock the tests move forward, e.g. past a link's expiry or the resend cooldown.
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
