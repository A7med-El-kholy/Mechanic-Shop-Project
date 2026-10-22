using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;

public sealed class RemoveWorkOrderCommandHandler(
    IAppDbContext context,
    ILogger<RemoveWorkOrderCommandHandler> logger)
    : IRequestHandler<RemoveWorkOrderCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveWorkOrderCommandHandler> _logger = logger;

    public async Task<Result<Deleted>> Handle(RemoveWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(w => w.Id == command.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        // Completed and cancelled orders are history. History gets cancelled or
        // closed out — never silently deleted.
        if (!workOrder.IsEditable)
        {
            return WorkOrderErrors.NotDeletable(workOrder.State);
        }

        _context.WorkOrders.Remove(workOrder);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Work order {WorkOrderId} removed.", command.WorkOrderId);

        return Result.Deleted;
    }
}
