namespace Orders.Service.Models;

public static class FieldLimits
{
    public const int MaxNameLength = 120;
    public const int MaxNotesLength = 1000;
    public const int MaxReasonLength = 500;
    public const int MaxCommentLength = 1000;
    public const int MoneyPrecision = 18;
    public const int MoneyScale = 2;
    public const decimal MaxPrice = 999_999.99m;
    public const int EventTypeMaxLength = 100;
    public const int RoutingKeyMaxLength = 255;
    public const int OutboxErrorMaxLength = 1000;
}
