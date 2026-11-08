using MongoDB.Bson;
using Analytics.Ingestor.Storage;

namespace Analytics.Ingestor.Catalog;

// The events say what happened, but not what the catalog looked like: a product's name and price, a farm's
// location. A snapshot stores one document per farm and per product, all with the same snapshotId and time, so the
// Silver layer can tell what changed from one day to the next (SCD type 2) and when a farm or product disappeared.
public sealed class CatalogSnapshotService
{
    public const string FarmEntity = "farm";
    public const string ProductEntity = "product";

    private readonly ICatalogReader _catalog;
    private readonly IRawStore _store;
    private readonly TimeProvider _timeProvider;

    public CatalogSnapshotService(ICatalogReader catalog, IRawStore store, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _store = store;
        _timeProvider = timeProvider;
    }

    // The next snapshot is due one interval after the last one, or now when there is none yet.
    public static DateTime NextDue(DateTime? lastSnapshotAt, DateTime now, TimeSpan interval) =>
        lastSnapshotAt is { } last && last + interval > now ? last + interval : now;

    public async Task<int> TakeAsync(CancellationToken cancellationToken)
    {
        // Both lists are read before anything is stored, so a snapshot is never half written because Farms failed.
        var farms = await _catalog.ListFarmsAsync(cancellationToken);
        var products = await _catalog.ListProductsAsync(cancellationToken);

        var snapshotId = Guid.NewGuid().ToString();
        var snapshotAt = _timeProvider.GetUtcNow().UtcDateTime;

        var documents = farms.Select(farm => Document(FarmEntity, farm))
            .Concat(products.Select(product => Document(ProductEntity, product)))
            .ToList();

        await _store.InsertSnapshotAsync(documents, cancellationToken);
        return documents.Count;

        BsonDocument Document(string entity, BsonDocument data) => new()
        {
            ["snapshotId"] = snapshotId,
            ["snapshotAt"] = snapshotAt,
            ["entity"] = entity,
            ["entityId"] = data.TryGetValue("id", out var id) ? id : BsonNull.Value,
            ["data"] = data
        };
    }
}
