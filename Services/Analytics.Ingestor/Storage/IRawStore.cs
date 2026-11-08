using MongoDB.Bson;

namespace Analytics.Ingestor.Storage;

// The raw zone (landing zone) of the analytics platform. Both methods return false when the message was already
// stored, so a redelivery changes nothing.
public interface IRawStore
{
    Task<bool> InsertReadingAsync(BsonDocument reading, string messageId, CancellationToken cancellationToken);

    Task<bool> InsertEventAsync(BsonDocument domainEvent, CancellationToken cancellationToken);
}
