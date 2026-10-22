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
