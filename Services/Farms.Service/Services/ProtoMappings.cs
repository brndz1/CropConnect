using Farms.Service.Application;
using Farms.Service.Models;
using Farms.Service.Protos;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace Farms.Service.Services;

public static class ProtoMappings
{
    #region Farms

    public static FarmInfo ToProto(this Farm farm)
    {
        var info = new FarmInfo
        {
            Id = farm.Id.ToString(),
            OwnerId = farm.OwnerId.ToString(),
            Name = farm.Name,
            Description = farm.Description,
            Location = farm.Location,
            IsActive = farm.IsActive,
            Version = farm.Version,
            CreatedAt = farm.CreatedAt.ToTimestamp()
        };

        if (farm.UpdatedAt is { } updatedAt)
        {
            info.UpdatedAt = updatedAt.ToTimestamp();
        }

        return info;
    }

    public static ListFarmsResponse ToProto(this PagedResult<Farm> page)
    {
        var response = new ListFarmsResponse
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };

        response.Farms.AddRange(page.Items.Select(farm => farm.ToProto()));
        return response;
    }

    #endregion

    #region Products

    public static ProductInfo ToProto(this Product product)
    {
        var info = new ProductInfo
        {
            Id = product.Id.ToString(),
            FarmId = product.FarmId.ToString(),
            FarmName = product.Farm?.Name ?? string.Empty,
            Name = product.Name,
            Description = product.Description,
            Unit = product.Unit,
            Price = product.Price.ToDecimalValue(),
            StockQuantity = product.StockQuantity,
            IsAvailable = product.IsAvailable,
            Version = product.Version,
            CreatedAt = product.CreatedAt.ToTimestamp()
        };

        if (product.UpdatedAt is { } updatedAt)
        {
            info.UpdatedAt = updatedAt.ToTimestamp();
        }

        return info;
    }

    public static ListProductsResponse ToProto(this PagedResult<Product> page)
    {
        var response = new ListProductsResponse
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };

        response.Products.AddRange(page.Items.Select(product => product.ToProto()));
        return response;
    }

    #endregion

    #region Stock

    public static StockReservationResponse ToProto(this StockReservationResult result)
    {
        var response = new StockReservationResponse
        {
            OrderId = result.OrderId.ToString(),
            State = result.Status.ToProto(),
            ExpiresAt = result.ExpiresAt.ToTimestamp(),
            AlreadyProcessed = result.AlreadyProcessed
        };

        response.Items.AddRange(result.Lines.Select(line => new ReservedItem
        {
            ProductId = line.ProductId.ToString(),
            ProductName = line.ProductName,
            FarmId = line.FarmId.ToString(),
            FarmOwnerId = line.FarmOwnerId.ToString(),
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice.ToDecimalValue()
        }));

        return response;
    }

    public static ReservationState ToProto(this ReservationStatus status) => status switch
    {
        ReservationStatus.Reserved => ReservationState.Reserved,
        ReservationStatus.Confirmed => ReservationState.Confirmed,
        ReservationStatus.Released => ReservationState.Released,
        ReservationStatus.Expired => ReservationState.Expired,
        _ => ReservationState.Unspecified
    };

    #endregion

    #region Helpers

    private static Timestamp ToTimestamp(this DateTime value) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    #endregion
}
