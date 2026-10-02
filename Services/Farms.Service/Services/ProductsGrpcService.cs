using Farms.Service.Application;
using Farms.Service.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Farms.Service.Services;

[Authorize]
public sealed class ProductsGrpcService : ProductsService.ProductsServiceBase
{
    #region Dependencies

    private readonly IProductApplicationService _products;

    public ProductsGrpcService(IProductApplicationService products)
    {
        _products = products;
    }

    #endregion

    #region Queries

    [AllowAnonymous]
    public override async Task<ProductInfo> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        var productId = RequestParsing.ParseId(request.Id, "Product ID");
        var product = await _products.GetAsync(context.GetCaller(), productId, context.CancellationToken);
        return product.ToProto();
    }

    [AllowAnonymous]
    public override async Task<ListProductsResponse> ListProducts(ListProductsRequest request, ServerCallContext context)
    {
        var query = new ProductQuery(
            PageRequest.Create(request.Page, request.PageSize),
            RequestParsing.ParseOptionalId(request.HasFarmId, request.FarmId, "Farm ID"),
            RequestParsing.OptionalText(request.HasSearch, request.Search),
            request.OnlyInStock,
            request.IncludeUnavailable);

        var page = await _products.ListAsync(context.GetCaller(), query, context.CancellationToken);
        return page.ToProto();
    }

    #endregion

    #region Commands

    public override async Task<ProductInfo> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        var command = new CreateProductCommand(
            RequestParsing.ParseId(request.FarmId, "Farm ID"),
            request.Name,
            request.Description,
            request.Unit,
            RequestParsing.RequiredDecimal(request.Price, "Price"),
            request.StockQuantity);

        var product = await _products.CreateAsync(context.GetCaller(), command, context.CancellationToken);
        return product.ToProto();
    }

    public override async Task<ProductInfo> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
    {
        var command = new UpdateProductCommand(
            RequestParsing.ParseId(request.Id, "Product ID"),
            RequestParsing.OptionalText(request.HasName, request.Name),
            RequestParsing.OptionalText(request.HasDescription, request.Description),
            RequestParsing.OptionalText(request.HasUnit, request.Unit),
            RequestParsing.OptionalDecimal(request.Price, "Price"),
            RequestParsing.OptionalFlag(request.HasIsAvailable, request.IsAvailable),
            RequestParsing.OptionalNumber(request.HasExpectedVersion, request.ExpectedVersion));

        var product = await _products.UpdateAsync(context.GetCaller(), command, context.CancellationToken);
        return product.ToProto();
    }

    public override async Task<ProductInfo> AdjustStock(AdjustStockRequest request, ServerCallContext context)
    {
        var productId = RequestParsing.ParseId(request.Id, "Product ID");
        var product = await _products.AdjustStockAsync(context.GetCaller(), productId, request.Delta, context.CancellationToken);
        return product.ToProto();
    }

    public override async Task<Empty> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
    {
        var productId = RequestParsing.ParseId(request.Id, "Product ID");
        await _products.DeleteAsync(context.GetCaller(), productId, context.CancellationToken);
        return new Empty();
    }

    #endregion
}
