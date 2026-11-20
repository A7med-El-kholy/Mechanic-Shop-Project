using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Parts.Commands.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandHandler(IAppDbContext context)
    : IRequestHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    public async Task<Result<PurchaseOrderDto>> Handle(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers.FirstOrDefaultAsync(item => item.Id == command.SupplierId, cancellationToken);
        if (supplier is null) return SupplierErrors.NotFound(command.SupplierId);
        if (!supplier.IsActive) return SupplierErrors.Inactive;
        if (command.Items is null || command.Items.Count == 0) return PurchaseOrderErrors.LinesRequired;
        if (command.Items.Select(item => item.PartId).Distinct().Count() != command.Items.Count)
            return PurchaseOrderErrors.DuplicatePart;

        var partIds = command.Items.Select(item => item.PartId).ToArray();
        var parts = await context.Parts.Where(part => partIds.Contains(part.Id)).ToListAsync(cancellationToken);
        if (parts.Count != partIds.Length) return PartErrors.NotFound(partIds.Except(parts.Select(part => part.Id)).First());

        var lines = new List<PurchaseOrderLine>(command.Items.Count);
        foreach (var item in command.Items)
        {
            var part = parts.Single(part => part.Id == item.PartId);
            var createdLine = PurchaseOrderLine.Create(part.Id, part.Name, item.Quantity, item.UnitCost);
            if (createdLine.IsError) return createdLine.Errors;
            lines.Add(createdLine.Value);
        }

        var created = PurchaseOrder.Create(Guid.NewGuid(), PurchaseOrder.NewNumber(), supplier.Id,
            DateOnly.FromDateTime(DateTime.UtcNow), lines);
        if (created.IsError) return created.Errors;

        context.PurchaseOrders.Add(created.Value);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(created.Value, supplier.Name);
    }

    internal static PurchaseOrderDto ToDto(PurchaseOrder order, string supplierName) =>
        new(order.Id, order.Number, order.SupplierId, supplierName, order.OrderedOn, order.ReceivedOn,
            (int)order.Status, order.Subtotal, order.Lines.Select(line => new PurchaseOrderLineDto(
                line.PartId, line.PartName, line.Quantity, line.UnitCost, line.UnitCost * line.Quantity)).ToList());
}
