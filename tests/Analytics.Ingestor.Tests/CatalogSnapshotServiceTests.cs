using MongoDB.Bson;
using Analytics.Ingestor.Catalog;

namespace Analytics.Ingestor.Tests;

public sealed class CatalogSnapshotServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeRawStore _store = new();
    private readonly FakeCatalog _catalog = new();

    private CatalogSnapshotService Service() => new(_catalog, _store, new FixedClock(Now));

    [Fact]
    public async Task A_snapshot_stores_every_farm_and_product_as_returned_under_one_id_and_time()
    {
        _catalog.Farms.Add(BsonDocument.Parse("""{"id":"f1","name":"Quinta do Vale","isActive":true}"""));
        _catalog.Products.Add(BsonDocument.Parse("""{"id":"p1","farmId":"f1","name":"Alface","price":{"units":"1","nanos":500000000},"stockQuantity":0}"""));
        _catalog.Products.Add(BsonDocument.Parse("""{"id":"p2","farmId":"f1","name":"Cenoura"}"""));

        Assert.Equal(3, await Service().TakeAsync(CancellationToken.None));

        Assert.Equal(3, _store.Snapshots.Count);
        Assert.Single(_store.Snapshots.Select(s => s["snapshotId"].AsString).Distinct());
        Assert.All(_store.Snapshots, s => Assert.Equal(Now, s["snapshotAt"].ToUniversalTime()));

        var farm = Assert.Single(_store.Snapshots, s => s["entity"] == "farm");
        Assert.Equal("f1", farm["entityId"].AsString);
        Assert.Equal("Quinta do Vale", farm["data"]["name"].AsString);

        var lettuce = Assert.Single(_store.Snapshots, s => s["entityId"] == "p1");
        Assert.Equal("product", lettuce["entity"].AsString);
        Assert.Equal(0, lettuce["data"]["stockQuantity"].AsInt32);
    }

    [Fact]
    public async Task Nothing_is_stored_when_the_catalog_cannot_be_read()
    {
        _catalog.Farms.Add(BsonDocument.Parse("""{"id":"f1"}"""));
        _catalog.FailProducts = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().TakeAsync(CancellationToken.None));
        Assert.Empty(_store.Snapshots);
    }

    [Fact]
    public void The_first_snapshot_is_due_at_once_and_the_next_one_an_interval_later()
    {
        var day = TimeSpan.FromDays(1);

        Assert.Equal(Now, CatalogSnapshotService.NextDue(null, Now, day));
        Assert.Equal(Now.AddHours(18), CatalogSnapshotService.NextDue(Now.AddHours(-6), Now, day));
        // Missed while the service was down: due now, not in the past.
        Assert.Equal(Now, CatalogSnapshotService.NextDue(Now.AddDays(-3), Now, day));
    }

    private sealed class FakeCatalog : ICatalogReader
    {
        public List<BsonDocument> Farms { get; } = [];

        public List<BsonDocument> Products { get; } = [];

        public bool FailProducts { get; set; }

        public Task<IReadOnlyList<BsonDocument>> ListFarmsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BsonDocument>>(Farms);

        public Task<IReadOnlyList<BsonDocument>> ListProductsAsync(CancellationToken cancellationToken) =>
            FailProducts
                ? throw new InvalidOperationException("Farms is down.")
                : Task.FromResult<IReadOnlyList<BsonDocument>>(Products);
    }
}
