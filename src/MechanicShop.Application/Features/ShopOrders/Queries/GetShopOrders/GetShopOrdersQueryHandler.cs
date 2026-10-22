using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Application.Features.Parts.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.ShopOrders.Queries.GetShopOrders;

public sealed class GetShopOrdersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetShopOrdersQuery, Result<PaginatedList<PartOrderDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<PartOrderDto>>> Handle(GetShopOrdersQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var ordersQuery = _context.ShopOrders
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .AsNoTracking();

        var count = await ordersQuery.CountAsync(cancellationToken);

        var items = await ordersQuery
            .OrderByDescending(o => o.PlacedOn)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<PartOrderDto>(
            items.ToDtos(),
            page,
            pageSize,
            count);
    }
}
