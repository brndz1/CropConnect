using Microsoft.EntityFrameworkCore;

namespace Sensors.Service.Data;

// With several replicas of the service (Kubernetes), each background worker runs in all of them. A PostgreSQL
// advisory lock lets one replica at a time do a round, so events keep their order and no email goes out twice.
public static class WorkerLock
{
    public const long OutboxPublisher = 1;

    // Null when another replica holds the lock: skip this round. The lock is held until the result is disposed,
    // and PostgreSQL drops it on its own if the replica dies.
    public static async Task<IAsyncDisposable?> TryAcquireAsync(DbContext dbContext, long key, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return NoLock.Instance;
        }

        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        var acquired = await dbContext.Database
            .SqlQuery<bool>($"SELECT pg_try_advisory_lock({key}) AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (acquired)
        {
            return new HeldLock(dbContext, key);
        }

        await dbContext.Database.CloseConnectionAsync();
        return null;
    }

    private sealed class HeldLock(DbContext dbContext, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({key})");
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private sealed class NoLock : IAsyncDisposable
    {
        public static readonly NoLock Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
