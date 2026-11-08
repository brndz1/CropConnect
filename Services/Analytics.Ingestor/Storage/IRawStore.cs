using MongoDB.Bson;

namespace Analytics.Ingestor.Storage;

// The raw zone (landing zone) of the analytics platform. Storing a reading or an event returns false when it was
// already stored, so a redelivery changes nothing.
public interface IRawStore
{
    Task<bool> InsertReadingAsync(BsonDocument reading, string messageId, CancellationToken cancellationToken);

    Task<bool> InsertEventAsync(BsonDocument domainEvent, CancellationToken cancellationToken);

    Task InsertSnapshotAsync(IReadOnlyCollection<BsonDocument> documents, CancellationToken cancellationToken);

    // Null while no catalog snapshot was taken yet.
    Task<DateTime?> GetLatestSnapshotTimeAsync(CancellationToken cancellationToken);
}
