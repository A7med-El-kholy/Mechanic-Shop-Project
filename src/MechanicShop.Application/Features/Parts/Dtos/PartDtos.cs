namespace MechanicShop.Application.Features.Parts.Dtos;

/// <summary>A shelf row for both the storeroom table and the customer shop —
/// the stock state is derived server-side so the two views cannot disagree.</summary>
public sealed record PartDto(
    Guid PartId,
    string Name,
    int Category,
    decimal Price,
    int Quantity,
    int MinimumQuantity,
    int StockState);

/// <summary>One purchased line as the buyer and manager see it: name/price as
/// charged (snapshots), quantity.</summary>
public sealed record PartOrderLineDto(
    Guid PartId,
    string PartName,
    int Quantity,
    decimal UnitPrice);

/// <summary>The parts receipt: who, when, the money math, and the payment story.</summary>
public sealed record PartOrderDto(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    DateOnly PlacedOn,
    int Status,
    int? PaymentMethod,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    List<PartOrderLineDto> Lines);

public sealed record StockMovementDto(
    Guid MovementId,
    Guid PartId,
    int Delta,
    int QuantityBefore,
    int QuantityAfter,
    string Reason,
    string? ActorUserId,
    DateTimeOffset RecordedAt);

public sealed record SupplierDto(Guid SupplierId, string Name, string? ContactName, string? Email, string? Phone, bool IsActive);

public sealed record PurchaseOrderLineDto(Guid PartId, string PartName, int Quantity, decimal UnitCost, decimal LineTotal);

public sealed record PurchaseOrderDto(
    Guid PurchaseOrderId,
    string Number,
    Guid SupplierId,
    string SupplierName,
    DateOnly OrderedOn,
    DateOnly? ReceivedOn,
    int Status,
    decimal Subtotal,
    List<PurchaseOrderLineDto> Lines);
