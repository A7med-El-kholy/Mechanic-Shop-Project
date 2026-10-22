using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;

using MediatR;

using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetMyRepairById;

public sealed class GetMyRepairByIdQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetMyRepairByIdQuery, Result<WorkOrderDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<WorkOrderDto>> Handle(GetMyRepairByIdQuery query, CancellationToken cancellationToken)
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

        var workOrder = await _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .Include(w => w.Technician)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.Id == query.WorkOrderId && w.Vehicle != null && w.Vehicle.CustomerId == customer.Id,
                cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(query.WorkOrderId);
        }

        return workOrder.ToDto();
    }
}
