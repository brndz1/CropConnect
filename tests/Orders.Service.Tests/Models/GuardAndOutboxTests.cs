using System.Globalization;
using Orders.Service.Application;
using Orders.Service.Models;

namespace Orders.Service.Tests.Models;

public sealed class GuardAndOutboxTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("2.5")]
    [InlineData("999999.99")]
    public void Valid_prices_pass(string price)
    {
        Assert.Equal(decimal.Parse(price, CultureInfo.InvariantCulture), Guard.Price(decimal.Parse(price, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1000000")]
    [InlineData("1.005")]
    public void Negative_huge_or_sub_cent_prices_are_rejected(string price)
    {
        Assert.Throws<InvalidInputException>(() => Guard.Price(decimal.Parse(price, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rating_must_be_one_to_five(int rating)
    {
        Assert.Throws<InvalidInputException>(() => Guard.Rating(rating));
    }

    [Fact]
    public void Quantity_must_be_positive()
    {
        Assert.Throws<InvalidInputException>(() => OrderItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Alface", 0, 1m));
    }

    [Fact]
    public void Item_total_is_quantity_times_unit_price()
    {
        var item = OrderItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Alface", 3, 1.25m);

        Assert.Equal(3.75m, item.TotalPrice);
    }

    [Fact]
    public void Text_over_the_limit_is_rejected()
    {
        Assert.Throws<InvalidInputException>(() => Guard.Optional(new string('x', 11), "Notes", 10));
        Assert.Equal(string.Empty, Guard.Optional("   ", "Notes", 10));
    }

    [Fact]
    public void Outbox_keeps_only_a_valid_length_trace_parent()
    {
        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        Assert.Equal(traceParent, OutboxMessage.Create("OrderCreated", "order.created", "{}", DateTime.UtcNow, traceParent).TraceParent);
        Assert.Null(OutboxMessage.Create("OrderCreated", "order.created", "{}", DateTime.UtcNow, traceParent + "-extra").TraceParent);
    }

    [Fact]
    public void Outbox_counts_attempts_and_truncates_long_errors()
    {
        var message = OutboxMessage.Create("OrderCreated", "order.created", "{}", DateTime.UtcNow);

        message.MarkFailed(new string('e', 5000));
        Assert.Equal(1, message.Attempts);
        Assert.Equal(FieldLimits.OutboxErrorMaxLength, message.LastError!.Length);

        message.MarkPublished(DateTime.UtcNow);
        Assert.Equal(2, message.Attempts);
        Assert.NotNull(message.PublishedAt);
        Assert.Null(message.LastError);
    }

    [Theory]
    [InlineData(0, 0, 1, PageRequest.DefaultPageSize)]
    [InlineData(-3, 500, 1, PageRequest.MaxPageSize)]
    [InlineData(3, 10, 3, 10)]
    public void Page_request_is_clamped(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var request = PageRequest.Create(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedSize, request.PageSize);
    }
}
