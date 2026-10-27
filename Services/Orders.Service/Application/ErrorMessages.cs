namespace Orders.Service.Application;

public static class ErrorMessages
{
    public const string OrderNotFound = "Order not found.";
    public const string ReviewNotFound = "Review not found.";
    public const string NotAllowedToViewOrder = "You are not allowed to view this order.";
    public const string NotAllowedToManageOrder = "You are not allowed to manage this order.";
    public const string NotAllowedToCancelOrder = "You are not allowed to cancel this order.";
    public const string OnlyClientsCanCreateOrders = "Only clients can create orders.";
    public const string OrderItemsRequired = "An order must contain at least one item.";
    public const string OrderAlreadyReviewed = "This order has already been reviewed.";
    public const string OnlyCompletedOrdersCanBeReviewed = "Only picked up orders can be reviewed.";
    public const string OnlyCustomerCanReviewOrder = "Only the customer who placed the order can submit a review.";
}
