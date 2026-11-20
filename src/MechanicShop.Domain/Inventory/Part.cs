namespace MechanicShop.Domain.Inventory;

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

/// <summary>
/// One shelf entry in the storeroom and the shop: what it is, what it costs,
/// how many are on the shelf, and the level where the manager must reorder.
/// The three stock states are DERIVED (empty / below minimum / healthy) — never
/// stored — so they can never drift out of sync with the numbers.
/// </summary>
public sealed class Part : Entity
{
    public string Name { get; private set; }

    public PartCategory Category { get; private set; }

    public decimal Price { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Reorder level: at or below this the shelf is "Low stock".</summary>
    public int MinimumQuantity { get; private set; }

    /// <summary>SQL Server concurrency token protecting competing stock updates.</summary>
    public byte[] RowVersion { get; private set; } = [];

#pragma warning disable CS8618

    private Part()
    { }

#pragma warning restore CS8618

    private Part(Guid id, string name, PartCategory category, decimal price, int quantity, int minimumQuantity)
        : base(id)
    {
        Name = name;
        Category = category;
        Price = price;
        Quantity = quantity;
        MinimumQuantity = minimumQuantity;
    }

    public PartStockState StockState => Quantity == 0
        ? PartStockState.OutOfStock
        : Quantity <= MinimumQuantity
            ? PartStockState.LowStock
            : PartStockState.InStock;

    public static Result<Part> Create(
        Guid id,
        string name,
        int category,
        decimal price,
        int quantity,
        int minimumQuantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PartErrors.NameRequired;
        }

        if (name.Trim().Length > 100)
        {
            return PartErrors.NameTooLong;
        }

        if (price <= 0 || price > 10_000)
        {
            return PartErrors.PriceInvalid;
        }

        if (quantity < 0)
        {
            return PartErrors.QuantityInvalid;
        }

        if (minimumQuantity < 0)
        {
            return PartErrors.MinimumInvalid;
        }

        if (Enum.IsDefined(typeof(PartCategory), category) is false)
        {
            return PartErrors.CategoryInvalid;
        }

        return new Part(id, name.Trim(), (PartCategory)category, price, quantity, minimumQuantity);
    }

    /// <summary>Adds or removes stock. An outbound movement that exceeds available
    /// stock is rejected so the transaction history and shelf quantity stay aligned.</summary>
    public Result<Updated> Adjust(int delta)
    {
        if (delta == 0)
        {
            return PartErrors.DeltaInvalid;
        }

        var next = (long)Quantity + delta;
        if (next < 0 || next > int.MaxValue)
        {
            return PartErrors.QuantityInvalid;
        }

        Quantity = (int)next;

        return Result.Updated;
    }
}
