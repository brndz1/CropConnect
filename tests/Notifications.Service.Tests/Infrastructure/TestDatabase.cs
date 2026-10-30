using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notifications.Service.Data;

namespace Notifications.Service.Tests.Infrastructure;

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

    public NotificationsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
