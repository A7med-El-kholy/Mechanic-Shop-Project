using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Inventory;

public static class PurchaseOrderErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("PurchaseOrder_NotFound", $"Purchase order '{id}' was not found.");
    public static Error SupplierRequired => Error.Validation("PurchaseOrder_SupplierRequired", "Select a supplier.");
    public static Error NumberInvalid => Error.Validation("PurchaseOrder_NumberInvalid", "Purchase order number is required and cannot exceed 20 characters.");
    public static Error OrderedOnRequired => Error.Validation("PurchaseOrder_OrderedOnRequired", "Order date is required.");
    public static Error ReceivedOnRequired => Error.Validation("PurchaseOrder_ReceivedOnRequired", "Receipt date is required.");
    public static Error LinesRequired => Error.Validation("PurchaseOrder_LinesRequired", "A purchase order needs at least one item.");
    public static Error LineInvalid => Error.Validation("PurchaseOrder_LineInvalid", "Each line must reference a valid part with a name.");
    public static Error QuantityInvalid => Error.Validation("PurchaseOrder_QuantityInvalid", "Each line quantity must be between 1 and 100,000.");
    public static Error UnitCostInvalid => Error.Validation("PurchaseOrder_UnitCostInvalid", "Unit cost must be positive and no more than 100,000.");
    public static Error DuplicatePart => Error.Validation("PurchaseOrder_DuplicatePart", "A part can appear only once on a purchase order.");
    public static Error AlreadyReceived => Error.Conflict("PurchaseOrder_AlreadyReceived", "This purchase order has already been received.");
    public static Error StockChanged => Error.Conflict("PurchaseOrder_StockChanged", "Inventory or purchase order changed during receipt. Refresh and try again.");
}
