using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderById;

public sealed class GetWorkOrderByIdQueryHandler(IAppDbContext context, IUser caller)
    : IRequestHandler<GetWorkOrderByIdQuery, Result<WorkOrderDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _caller = caller;

    public async Task<Result<WorkOrderDto>> Handle(GetWorkOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var scope = await StaffScope.ResolveMineAsync(_context, _caller, cancellationToken);

        if (scope.IsError)
        {
            return scope.Errors;
        }

        var mine = scope.Value.TechnicianId;

        var workOrder = await _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .Include(w => w.Technician)
            .AsNoTracking()
            // For a technician the ticket simply does not exist unless it is theirs:
            // a cross-technician id is 404, not 403, so probing ids reveals nothing.
            .FirstOrDefaultAsync(
                w => w.Id == query.WorkOrderId && (mine == null || w.TechnicianId == mine),
                cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(query.WorkOrderId);
        }

        return workOrder.ToDto();
    }
}
