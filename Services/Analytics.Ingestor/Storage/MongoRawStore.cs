using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Analytics.Ingestor.Storage;

// The cropconnect_raw database:
//   sensor_readings  time-series collection (timeField "timestamp", metaField "meta" = farm, sensor and type):
//                    MongoDB groups the readings of each sensor in buckets and compresses them.
//   domain_events    one document per event, _id = the producer's MessageId.
//   catalog_snapshots one document per farm and per product of each daily snapshot of the public catalog.
// The collections and indexes are created on first use, so MongoDB may start after this service.
public sealed class MongoRawStore : IRawStore
{
    public const string ReadingsCollection = "sensor_readings";
    public const string EventsCollection = "domain_events";
    public const string SnapshotsCollection = "catalog_snapshots";

    #region Dependencies

    private readonly MongoOptions _options;
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private volatile bool _ready;

    public MongoRawStore(IOptions<MongoOptions> options)
    {
        _options = options.Value;
        Database = new MongoClient(_options.ConnectionString).GetDatabase(_options.Database);
    }

    public IMongoDatabase Database { get; }

    private IMongoCollection<BsonDocument> Readings => Database.GetCollection<BsonDocument>(ReadingsCollection);

    private IMongoCollection<BsonDocument> Events => Database.GetCollection<BsonDocument>(EventsCollection);

    private IMongoCollection<BsonDocument> Snapshots => Database.GetCollection<BsonDocument>(SnapshotsCollection);

    #endregion

    #region Writes

    // A time-series collection cannot have a unique index, so a redelivered reading is looked up by its messageId
    // first. With one ingestor this is enough; the Silver layer removes any duplicate that could still get through.
    public async Task<bool> InsertReadingAsync(BsonDocument reading, string messageId, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        var exists = await Readings
            .Find(Builders<BsonDocument>.Filter.Eq("messageId", messageId))
            .Limit(1)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            return false;
        }

        await Readings.InsertOneAsync(reading, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> InsertEventAsync(BsonDocument domainEvent, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        try
        {
            await Events.InsertOneAsync(domainEvent, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task InsertSnapshotAsync(IReadOnlyCollection<BsonDocument> documents, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        if (documents.Count > 0)
        {
            await Snapshots.InsertManyAsync(documents, cancellationToken: cancellationToken);
        }
    }

    public async Task<DateTime?> GetLatestSnapshotTimeAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        var latest = await Snapshots.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("snapshotAt"))
            .Project(Builders<BsonDocument>.Projection.Include("snapshotAt"))
            .FirstOrDefaultAsync(cancellationToken);

        return latest?["snapshotAt"].ToUniversalTime();
    }

    #endregion

    #region Setup

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return;
        }

        await _setupLock.WaitAsync(cancellationToken);

        try
        {
            if (_ready)
            {
                return;
            }

            var existing = await (await Database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken);

            if (!existing.Contains(ReadingsCollection))
            {
                await CreateReadingsCollectionAsync(cancellationToken);
            }

            // Creating an index that already exists does nothing.
            await Readings.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("messageId")),
                    // Databricks copies what arrived since its last run (incremental extraction).
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("receivedAt"))
                ],
                cancellationToken);

            await Events.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("receivedAt")),
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("routingKey").Ascending("receivedAt"))
                ],
                cancellationToken);

            await Snapshots.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("snapshotAt")),
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("entity").Ascending("entityId"))
                ],
                cancellationToken);

            _ready = true;
        }
        finally
        {
            _setupLock.Release();
        }
    }

    private async Task CreateReadingsCollectionAsync(CancellationToken cancellationToken)
    {
        var options = new CreateCollectionOptions
        {
            TimeSeriesOptions = new TimeSeriesOptions("timestamp", "meta", TimeSeriesGranularity.Seconds)
        };

        if (_options.ReadingsRetention is { } retention)
        {
            options.ExpireAfter = retention;
        }

        try
        {
            await Database.CreateCollectionAsync(ReadingsCollection, options, cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.CodeName == "NamespaceExists")
        {
            // Created in the meantime by another replica.
        }
    }

    #endregion
}
