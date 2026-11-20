using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Commands.CreatePurchaseOrder;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Parts.Commands.ReceivePurchaseOrder;

public sealed class ReceivePurchaseOrderCommandHandler(
    IAppDbContext context, ILogger<ReceivePurchaseOrderCommandHandler> logger)
    : IRequestHandler<ReceivePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    public async Task<Result<PurchaseOrderDto>> Handle(ReceivePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await context.PurchaseOrders.Include(item => item.Supplier).Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == command.PurchaseOrderId, cancellationToken);
        if (order is null) return PurchaseOrderErrors.NotFound(command.PurchaseOrderId);
        if (order.Status == PurchaseOrderStatus.Received) return PurchaseOrderErrors.AlreadyReceived;

        var partIds = order.Lines.Select(line => line.PartId).ToArray();
        var parts = await context.Parts.Where(part => partIds.Contains(part.Id)).ToDictionaryAsync(part => part.Id, cancellationToken);
        if (parts.Count != partIds.Length) return PartErrors.NotFound(partIds.First(id => !parts.ContainsKey(id)));

        foreach (var line in order.Lines)
        {
            var part = parts[line.PartId];
            var quantityBefore = part.Quantity;
            var received = part.Adjust(line.Quantity);
            if (received.IsError) return received.Errors;
            context.StockMovements.Add(StockMovement.Create(part.Id, line.Quantity, quantityBefore,
                part.Quantity, $"Purchase order {order.Number} received", command.ActorUserId));
        }

        var marked = order.MarkReceived(DateOnly.FromDateTime(DateTime.UtcNow));
        if (marked.IsError) return marked.Errors;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogInformation(exception, "Purchase order {PurchaseOrderId} receipt conflicted with another update.", order.Id);
            return PurchaseOrderErrors.StockChanged;
        }

        return CreatePurchaseOrderCommandHandler.ToDto(order, order.Supplier?.Name ?? "Unknown supplier");
    }
}
