using MongoDB.Bson;
using Analytics.Ingestor.Storage;

namespace Analytics.Ingestor.Tests;

// Keeps the documents in memory with the same duplicate rules as MongoDB: readings by messageId, events by _id.
public sealed class FakeRawStore : IRawStore
{
    public List<BsonDocument> Readings { get; } = [];

    public List<BsonDocument> Events { get; } = [];

    public List<BsonDocument> Snapshots { get; } = [];

    public Task<bool> InsertReadingAsync(BsonDocument reading, string messageId, CancellationToken cancellationToken)
    {
        if (Readings.Any(r => r["messageId"] == messageId))
        {
            return Task.FromResult(false);
        }

        Readings.Add(reading);
        return Task.FromResult(true);
    }

    public Task<bool> InsertEventAsync(BsonDocument domainEvent, CancellationToken cancellationToken)
    {
        if (Events.Any(e => e["_id"] == domainEvent["_id"]))
        {
            return Task.FromResult(false);
        }

        Events.Add(domainEvent);
        return Task.FromResult(true);
    }

    public Task InsertSnapshotAsync(IReadOnlyCollection<BsonDocument> documents, CancellationToken cancellationToken)
    {
        Snapshots.AddRange(documents);
        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLatestSnapshotTimeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Snapshots.Count == 0 ? (DateTime?)null : Snapshots.Max(s => s["snapshotAt"].ToUniversalTime()));
}
