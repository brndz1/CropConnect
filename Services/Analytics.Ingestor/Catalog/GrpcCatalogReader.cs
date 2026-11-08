using Google.Protobuf;
using MongoDB.Bson;
using Farms.Service.Protos;

namespace Analytics.Ingestor.Catalog;

public interface ICatalogReader
{
    Task<IReadOnlyList<BsonDocument>> ListFarmsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BsonDocument>> ListProductsAsync(CancellationToken cancellationToken);
}

// Reads the public catalog through Farms' anonymous gRPC queries, the same ones the web app's catalog uses: the
// active farms and the products on sale. Each item is kept as the API returns it (protobuf's JSON mapping).
public sealed class GrpcCatalogReader : ICatalogReader
{
    private const int PageSize = 100;

    // Zero and false are kept too: a product out of stock must say stockQuantity 0, not leave it out.
    private static readonly JsonFormatter Formatter = new(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    private readonly FarmsService.FarmsServiceClient _farms;
    private readonly ProductsService.ProductsServiceClient _products;

    public GrpcCatalogReader(FarmsService.FarmsServiceClient farms, ProductsService.ProductsServiceClient products)
    {
        _farms = farms;
        _products = products;
    }

    public Task<IReadOnlyList<BsonDocument>> ListFarmsAsync(CancellationToken cancellationToken) =>
        ReadAllAsync<FarmInfo>(async page =>
        {
            var response = await _farms.ListFarmsAsync(new ListFarmsRequest { Page = page, PageSize = PageSize }, cancellationToken: cancellationToken);
            return ((IReadOnlyList<FarmInfo>)response.Farms, response.TotalCount);
        });

    public Task<IReadOnlyList<BsonDocument>> ListProductsAsync(CancellationToken cancellationToken) =>
        ReadAllAsync<ProductInfo>(async page =>
        {
            var response = await _products.ListProductsAsync(new ListProductsRequest { Page = page, PageSize = PageSize }, cancellationToken: cancellationToken);
            return ((IReadOnlyList<ProductInfo>)response.Products, response.TotalCount);
        });

    private static async Task<IReadOnlyList<BsonDocument>> ReadAllAsync<TItem>(Func<int, Task<(IReadOnlyList<TItem> Items, int Total)>> readPage)
        where TItem : IMessage
    {
        var all = new List<BsonDocument>();

        for (var page = 1; ; page++)
        {
            var (items, total) = await readPage(page);
            all.AddRange(items.Select(item => BsonDocument.Parse(Formatter.Format(item))));

            if (items.Count == 0 || all.Count >= total)
            {
                return all;
            }
        }
    }
}
