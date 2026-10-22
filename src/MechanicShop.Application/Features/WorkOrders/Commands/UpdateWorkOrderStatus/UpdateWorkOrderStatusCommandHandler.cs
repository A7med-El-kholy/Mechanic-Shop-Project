using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderStatus;

public sealed class UpdateWorkOrderStatusCommandHandler(
    IAppDbContext context,
    ILogger<UpdateWorkOrderStatusCommandHandler> logger)
    : IRequestHandler<UpdateWorkOrderStatusCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateWorkOrderStatusCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateWorkOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(w => w.Id == command.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        var updateResult = workOrder.UpdateState((WorkOrderState)command.State);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Work order {WorkOrderId} moved to {WorkOrderState}.",
            command.WorkOrderId,
            (WorkOrderState)command.State);

        return Result.Updated;
    }
}
