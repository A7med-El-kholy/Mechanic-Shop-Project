using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

public sealed class GetWorkOrdersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetWorkOrdersQuery, Result<PaginatedList<WorkOrderDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<WorkOrderDto>>> Handle(GetWorkOrdersQuery query, CancellationToken cancellationToken)
    {
        // Defensive input handling: the API accepts any ints, handlers decide bounds.
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var ordersQuery = _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .Include(w => w.Technician)
            .AsNoTracking()
            .AsQueryable();

        if (query.State is not null)
        {
            var state = (MechanicShop.Domain.WorkOrders.Enums.WorkOrderState)query.State;
            ordersQuery = ordersQuery.Where(w => w.State == state);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalized = query.Search.Trim().ToLower();

            // The list's one search box answers "which job is this?" — plate,
            // car and owner are all things a person at the counter knows.
            ordersQuery = ordersQuery.Where(w =>
                w.Vehicle != null &&
                (w.Vehicle.LicensePlate.ToLower().Contains(normalized) ||
                 w.Vehicle.Make.ToLower().Contains(normalized) ||
                 w.Vehicle.Model.ToLower().Contains(normalized) ||
                 (w.Vehicle.Customer != null &&
                  w.Vehicle.Customer.Name.ToLower().Contains(normalized))));
        }

        // Timeline order: the day's flow as a reader scans it, then stable ids for
        // Skip/Take paging.
        ordersQuery = ordersQuery
            .OrderBy(w => w.ScheduledDate)
            .ThenBy(w => w.StartsAtMinute)
            .ThenBy(w => w.Id);

        var count = await ordersQuery.CountAsync(cancellationToken);

        var items = await ordersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<WorkOrderDto>(
            items.ToDtos(),
            page,
            pageSize,
            count);
    }
}
