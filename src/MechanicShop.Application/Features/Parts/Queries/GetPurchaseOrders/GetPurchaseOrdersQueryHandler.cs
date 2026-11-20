using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Commands.CreatePurchaseOrder;
using MechanicShop.Application.Features.Parts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Parts.Queries.GetPurchaseOrders;

public sealed class GetPurchaseOrdersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetPurchaseOrdersQuery, List<PurchaseOrderDto>>
{
    public async Task<List<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = await context.PurchaseOrders.AsNoTracking()
            .Include(order => order.Supplier)
            .Include(order => order.Lines)
            .OrderByDescending(order => order.OrderedOn)
            .ThenByDescending(order => order.Number)
            .Take(200)
            .ToListAsync(cancellationToken);

        return orders.Select(order => CreatePurchaseOrderCommandHandler.ToDto(
            order, order.Supplier?.Name ?? "Unknown supplier")).ToList();
    }
}
