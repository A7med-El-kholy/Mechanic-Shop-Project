using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;

using MediatR;

using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetMyRepairs;

public sealed class GetMyRepairsQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetMyRepairsQuery, Result<PaginatedList<WorkOrderDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<PaginatedList<WorkOrderDto>>> Handle(GetMyRepairsQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            return AppointmentRequestErrors.NoProfileLinked;
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var repairsQuery = _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .Include(w => w.Technician)
            .AsNoTracking()
            .Where(w => w.Vehicle != null && w.Vehicle.CustomerId == customer.Id);

        var count = await repairsQuery.CountAsync(cancellationToken);

        var items = await repairsQuery
            .OrderByDescending(w => w.ScheduledDate)
            .ThenByDescending(w => w.StartsAtMinute)
            .ThenBy(w => w.Id)
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
