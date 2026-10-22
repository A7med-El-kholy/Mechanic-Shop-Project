namespace MechanicShop.Domain.Inventory;

using MechanicShop.Domain.Common.Results;

public static class PartOrderErrors
{
    public static Error NotFound(Guid orderId) =>
        Error.NotFound("ShopOrder_NotFound", $"Shop order '{orderId}' was not found.");

    public static Error AlreadyPaid =>
        Error.Conflict("ShopOrder_AlreadyPaid", "This shop order is already paid.");

    public static Error MethodInvalid =>
        Error.Validation("ShopOrder_MethodInvalid", "Payment method must be Card (0) or Cash (1).");

    public static Error ItemsRequired =>
        Error.Validation("ShopOrder_ItemsRequired", "A shop order needs at least one item.");

    public static Error QuantityInvalid =>
        Error.Validation("ShopOrder_QuantityInvalid", "Item quantity must be at least 1.");

    public static Error PartNotInStock(string partName) =>
        Error.Conflict(
            "ShopOrder_InsufficientStock",
            $"Not enough stock for '{partName}' to cover this order.");

    public static Error AlreadyChosen =>
        Error.Conflict(
            "ShopOrder_AlreadyCashPending",
            "Cash payment at the shop is already chosen for this order.");
}
