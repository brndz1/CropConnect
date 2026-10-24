namespace Orders.Service.Models;

public sealed class Review
{
    #region Properties

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid FarmId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Order? Order { get; private set; }

    #endregion

    #region Factory

    private Review()
    {
    }

    public static Review Create(
        Guid orderId,
        Guid customerId,
        Guid farmId,
        int rating,
        string? comment,
        DateTime now)
    {
        return new Review
        {
            Id = Guid.NewGuid(),
            OrderId = Guard.NotEmpty(orderId, nameof(OrderId)),
            CustomerId = Guard.NotEmpty(customerId, nameof(CustomerId)),
            FarmId = Guard.NotEmpty(farmId, nameof(FarmId)),
            Rating = Guard.Rating(rating),
            Comment = Guard.Optional(comment, nameof(Comment), FieldLimits.MaxCommentLength),
            CreatedAt = now
        };
    }

    #endregion
}
