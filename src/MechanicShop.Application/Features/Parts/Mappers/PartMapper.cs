using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Inventory;

namespace MechanicShop.Application.Features.Parts.Mappers;

public static class PartMapper
{
    public static PartDto ToDto(this Part part) => new(
        part.Id,
        part.Name,
        (int)part.Category,
        part.Price,
        part.Quantity,
        part.MinimumQuantity,
        (int)part.StockState);

    public static List<PartDto> ToDtos(this IEnumerable<Part> parts) =>
        [.. parts.Select(ToDto)];

    public static PartOrderDto ToDto(this PartOrder order) => new(
        order.Id,
        order.Number,
        order.CustomerId,
        order.Customer?.Name ?? string.Empty,
        order.PlacedOn,
        (int)order.Status,
        order.PaymentMethod is null ? null : (int)order.PaymentMethod,
        order.Subtotal,
        order.TaxRate,
        order.TaxAmount,
        order.TotalAmount,
        [.. order.Lines.Select(li => new PartOrderLineDto(li.PartId, li.PartName, li.Quantity, li.UnitPrice))]);

    public static List<PartOrderDto> ToDtos(this IEnumerable<PartOrder> orders) =>
        [.. orders.Select(ToDto)];
}
