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

        if (farm is { Latitude: { } latitude, Longitude: { } longitude })
        {
            info.Latitude = latitude;
            info.Longitude = longitude;
        }

        if (farm.PickupSchedules.Count > 0)
        {
            info.PickupSchedules.AddRange(farm.PickupSchedules.Select(s => s.ToProto()));
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

    #region Pickup Schedules

    public static PickupScheduleInfo ToProto(this PickupSchedule schedule)
    {
        var info = new PickupScheduleInfo
        {
            Id = schedule.Id.ToString(),
            FarmId = schedule.FarmId.ToString(),
            DayOfWeek = schedule.DayOfWeek.ToProto(),
            OpenTime = schedule.OpenTime.ToString("HH:mm"),
            CloseTime = schedule.CloseTime.ToString("HH:mm"),
            Version = schedule.Version,
            CreatedAt = schedule.CreatedAt.ToTimestamp()
        };

        if (schedule.UpdatedAt is { } updatedAt)
        {
            info.UpdatedAt = updatedAt.ToTimestamp();
        }

        return info;
    }

    public static ListPickupSchedulesResponse ToProto(this IEnumerable<PickupSchedule> schedules)
    {
        var response = new ListPickupSchedulesResponse();
        response.Schedules.AddRange(schedules.Select(s => s.ToProto()));
        return response;
    }

    public static Protos.DayOfWeek ToProto(this System.DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        System.DayOfWeek.Sunday => Protos.DayOfWeek.Sunday,
        System.DayOfWeek.Monday => Protos.DayOfWeek.Monday,
        System.DayOfWeek.Tuesday => Protos.DayOfWeek.Tuesday,
        System.DayOfWeek.Wednesday => Protos.DayOfWeek.Wednesday,
        System.DayOfWeek.Thursday => Protos.DayOfWeek.Thursday,
        System.DayOfWeek.Friday => Protos.DayOfWeek.Friday,
        System.DayOfWeek.Saturday => Protos.DayOfWeek.Saturday,
        _ => Protos.DayOfWeek.Unspecified
    };

    public static System.DayOfWeek ToDomain(this Protos.DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        Protos.DayOfWeek.Sunday => System.DayOfWeek.Sunday,
        Protos.DayOfWeek.Monday => System.DayOfWeek.Monday,
        Protos.DayOfWeek.Tuesday => System.DayOfWeek.Tuesday,
        Protos.DayOfWeek.Wednesday => System.DayOfWeek.Wednesday,
        Protos.DayOfWeek.Thursday => System.DayOfWeek.Thursday,
        Protos.DayOfWeek.Friday => System.DayOfWeek.Friday,
        Protos.DayOfWeek.Saturday => System.DayOfWeek.Saturday,
        _ => throw new InvalidInputException("A valid day of week must be specified.")
    };

    #endregion

    #region Products

    public static ProductInfo ToProto(this Product product, Caller caller)
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
            Type = product.Type.ToProto(),
            Version = product.Version,
            CreatedAt = product.CreatedAt.ToTimestamp()
        };

        if (product.UpdatedAt is { } updatedAt)
        {
            info.UpdatedAt = updatedAt.ToTimestamp();
        }

        if (product.AvailableUntil is { } availableUntil)
        {
            info.AvailableUntil = availableUntil.ToTimestamp();
        }

        if (product.Contents is { } contents && caller.CanSeeContents(product))
        {
            info.Contents = contents;
        }

        return info;
    }

    public static Protos.ProductType ToProto(this Models.ProductType type) => type switch
    {
        Models.ProductType.Product => Protos.ProductType.Product,
        Models.ProductType.PreDefinedBasket => Protos.ProductType.PreDefinedBasket,
        Models.ProductType.SurpriseBasket => Protos.ProductType.SurpriseBasket,
        _ => Protos.ProductType.Unspecified
    };

    public static Models.ProductType ToDomain(this Protos.ProductType type) => type switch
    {
        Protos.ProductType.PreDefinedBasket => Models.ProductType.PreDefinedBasket,
        Protos.ProductType.SurpriseBasket => Models.ProductType.SurpriseBasket,
        _ => Models.ProductType.Product
    };

    public static ListProductsResponse ToProto(this PagedResult<Product> page, Caller caller)
    {
        var response = new ListProductsResponse
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };

        response.Products.AddRange(page.Items.Select(product => product.ToProto(caller)));
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
