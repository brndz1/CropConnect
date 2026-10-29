using CropConnect.ApiGateway.Infrastructure;
using Farms.Service.Protos;
using Microsoft.AspNetCore.Http.HttpResults;
using ProtoProductType = Farms.Service.Protos.ProductType;

namespace CropConnect.ApiGateway.Endpoints;

// RF06, RF08, RF09: products and seasonal baskets in the catalog (Farms.Service).
public static class ProductEndpoints
{
    #region Contracts

    public enum ProductType
    {
        Product,
        PreDefinedBasket,
        SurpriseBasket
    }

    public sealed record ProductResponse(
        string Id,
        string FarmId,
        string FarmName,
        string Name,
        string Description,
        string Unit,
        decimal Price,
        int StockQuantity,
        bool IsAvailable,
        ProductType? Type,
        DateTime? AvailableUntil,
        DateTime? CreatedAt,
        DateTime? UpdatedAt,
        int Version);

    public sealed record CreateProductBody(
        string FarmId,
        string Name,
        string? Description,
        string Unit,
        decimal Price,
        int StockQuantity,
        ProductType Type,
        DateTime? AvailableUntil);

    public sealed record UpdateProductBody(
        string? Name,
        string? Description,
        string? Unit,
        decimal? Price,
        bool? IsAvailable,
        DateTime? AvailableUntil,
        int? ExpectedVersion);

    public sealed record AdjustStockBody(int Delta);

    #endregion

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder routes)
    {
        var products = routes.MapGroup("/api/products").WithTags("Products").RequireAuthorization();

        products.MapGet("/", async Task<Ok<PagedResponse<ProductResponse>>> (
                int? page, int? pageSize, string? farmId, string? search, bool? onlyInStock, bool? includeUnavailable, ProductType? type,
                ProductsService.ProductsServiceClient client, CancellationToken ct) =>
            {
                var request = new ListProductsRequest
                {
                    Page = page ?? 1,
                    PageSize = pageSize ?? 0,
                    OnlyInStock = onlyInStock ?? false,
                    IncludeUnavailable = includeUnavailable ?? false
                };
                if (farmId is not null) request.FarmId = farmId;
                if (search is not null) request.Search = search;
                if (type is not null) request.Type = ToProto(type.Value);

                var response = await client.ListProductsAsync(request, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<ProductResponse>(
                    response.Products.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize));
            })
            .AllowAnonymous()
            .WithSummary("Browse the catalog of products and baskets of every farm (RF09)");

        products.MapGet("/{id}", async Task<Ok<ProductResponse>> (string id, ProductsService.ProductsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetProductAsync(new GetProductRequest { Id = id }, cancellationToken: ct))))
            .AllowAnonymous()
            .WithSummary("Get a product or basket (RF09)");

        products.MapPost("/", async Task<Created<ProductResponse>> (CreateProductBody body, ProductsService.ProductsServiceClient client, CancellationToken ct) =>
            {
                var request = new CreateProductRequest
                {
                    FarmId = body.FarmId ?? "",
                    Name = body.Name ?? "",
                    Description = body.Description ?? "",
                    Unit = body.Unit ?? "",
                    Price = body.Price.ToFarmsDecimal(),
                    StockQuantity = body.StockQuantity,
                    Type = ToProto(body.Type)
                };
                if (body.AvailableUntil is not null) request.AvailableUntil = body.AvailableUntil.Value.ToTimestamp();

                var created = await client.CreateProductAsync(request, cancellationToken: ct);
                return TypedResults.Created($"/api/products/{created.Id}", ToResponse(created));
            })
            .WithSummary("Add a product or seasonal basket to your farm (manager, RF06, RF08)");

        products.MapPatch("/{id}", async Task<Ok<ProductResponse>> (string id, UpdateProductBody body, ProductsService.ProductsServiceClient client, CancellationToken ct) =>
            {
                var request = new UpdateProductRequest { Id = id };
                if (body.Name is not null) request.Name = body.Name;
                if (body.Description is not null) request.Description = body.Description;
                if (body.Unit is not null) request.Unit = body.Unit;
                if (body.Price is not null) request.Price = body.Price.Value.ToFarmsDecimal();
                if (body.IsAvailable is not null) request.IsAvailable = body.IsAvailable.Value;
                if (body.AvailableUntil is not null) request.AvailableUntil = body.AvailableUntil.Value.ToTimestamp();
                if (body.ExpectedVersion is not null) request.ExpectedVersion = body.ExpectedVersion.Value;
                return TypedResults.Ok(ToResponse(await client.UpdateProductAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Edit a product or basket (manager, RF06, RF08)");

        products.MapPost("/{id}/stock-adjustments", async Task<Ok<ProductResponse>> (string id, AdjustStockBody body, ProductsService.ProductsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.AdjustStockAsync(new AdjustStockRequest { Id = id, Delta = body.Delta }, cancellationToken: ct))))
            .WithSummary("Add or remove stock (manager, RF06)");

        products.MapDelete("/{id}", async Task<NoContent> (string id, ProductsService.ProductsServiceClient client, CancellationToken ct) =>
            {
                await client.DeleteProductAsync(new DeleteProductRequest { Id = id }, cancellationToken: ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Remove a product or basket (manager)");

        return routes;
    }

    #region Mapping

    private static ProductResponse ToResponse(ProductInfo product) => new(
        product.Id,
        product.FarmId,
        product.FarmName,
        product.Name,
        product.Description,
        product.Unit,
        product.Price.ToDecimal(),
        product.StockQuantity,
        product.IsAvailable,
        FromProto(product.Type),
        product.AvailableUntil.AsDateTime(),
        product.CreatedAt.AsDateTime(),
        product.UpdatedAt.AsDateTime(),
        product.Version);

    private static ProtoProductType ToProto(ProductType type) => type switch
    {
        ProductType.Product => ProtoProductType.Product,
        ProductType.PreDefinedBasket => ProtoProductType.PreDefinedBasket,
        ProductType.SurpriseBasket => ProtoProductType.SurpriseBasket,
        _ => ProtoProductType.Unspecified
    };

    private static ProductType? FromProto(ProtoProductType type) => type switch
    {
        ProtoProductType.Product => ProductType.Product,
        ProtoProductType.PreDefinedBasket => ProductType.PreDefinedBasket,
        ProtoProductType.SurpriseBasket => ProductType.SurpriseBasket,
        _ => null
    };

    #endregion
}
