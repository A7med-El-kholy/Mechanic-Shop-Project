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

/// <summary>Mirrors the invoice's payment story: card closes immediately,
/// cash reserves and the manager confirms receipt later.</summary>
public enum PartOrderStatus
{
    Paid = 0,
    CashPending = 1,
}
