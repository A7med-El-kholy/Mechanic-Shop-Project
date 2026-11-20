using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Inventory;

public enum PurchaseOrderStatus
{
    Open = 0,
    Received = 1,
}

/// <summary>A supplier order with frozen line prices and one-time receipt.</summary>
public sealed class PurchaseOrder
{
    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }
    public DateOnly OrderedOn { get; private set; }
    public DateOnly? ReceivedOn { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public List<PurchaseOrderLine> Lines { get; private set; } = [];
    public decimal Subtotal => Lines.Sum(line => line.UnitCost * line.Quantity);

    private PurchaseOrder() { }

    public static Result<PurchaseOrder> Create(Guid id, string number, Guid supplierId, DateOnly orderedOn,
        List<PurchaseOrderLine> lines)
    {
        if (id == Guid.Empty || supplierId == Guid.Empty) return PurchaseOrderErrors.SupplierRequired;
        if (string.IsNullOrWhiteSpace(number) || number.Length > 20) return PurchaseOrderErrors.NumberInvalid;
        if (orderedOn == default) return PurchaseOrderErrors.OrderedOnRequired;
        if (lines is null || lines.Count == 0) return PurchaseOrderErrors.LinesRequired;
        if (lines.Select(line => line.PartId).Distinct().Count() != lines.Count) return PurchaseOrderErrors.DuplicatePart;

        return new PurchaseOrder
        {
            Id = id,
            Number = number.Trim().ToUpperInvariant(),
            SupplierId = supplierId,
            OrderedOn = orderedOn,
            Status = PurchaseOrderStatus.Open,
            Lines = [.. lines],
        };
    }

    public Result<Updated> MarkReceived(DateOnly receivedOn)
    {
        if (Status == PurchaseOrderStatus.Received) return PurchaseOrderErrors.AlreadyReceived;
        if (receivedOn == default) return PurchaseOrderErrors.ReceivedOnRequired;
        Status = PurchaseOrderStatus.Received;
        ReceivedOn = receivedOn;
        return Result.Updated;
    }

    public static string NewNumber() => "PO-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid PartId { get; private set; }
    public string PartName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }

    private PurchaseOrderLine() { }

    public static Result<PurchaseOrderLine> Create(Guid partId, string partName, int quantity, decimal unitCost)
    {
        if (partId == Guid.Empty || string.IsNullOrWhiteSpace(partName) || partName.Length > 100)
            return PurchaseOrderErrors.LineInvalid;
        if (quantity is < 1 or > 100_000) return PurchaseOrderErrors.QuantityInvalid;
        if (unitCost is <= 0 or > 100_000) return PurchaseOrderErrors.UnitCostInvalid;
        return new PurchaseOrderLine
        {
            Id = Guid.NewGuid(), PartId = partId, PartName = partName.Trim(), Quantity = quantity, UnitCost = unitCost,
        };
    }
}
