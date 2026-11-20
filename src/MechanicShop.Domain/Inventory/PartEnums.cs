namespace MechanicShop.Domain.Inventory;

/// <summary>Aisle in the parts shop; "All" is a UI filter, not a value.</summary>
public enum PartCategory
{
    Engine = 0,
    Brakes = 1,
    Electrical = 2,
    Fluids = 3,
}

/// <summary>Derived, never stored: empty shelf, thin shelf, healthy shelf.</summary>
public enum PartStockState
{
    InStock = 0,
    LowStock = 1,
    OutOfStock = 2,
}

/// <summary>Cash reservations remain pending until the workshop confirms receipt.
/// Paid is recorded only after payment is actually received.</summary>
public enum PartOrderStatus
{
    Paid = 0,
    CashPending = 1,
}
