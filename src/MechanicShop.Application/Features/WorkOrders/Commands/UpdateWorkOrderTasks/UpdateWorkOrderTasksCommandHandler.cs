using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderTasks;

public sealed class UpdateWorkOrderTasksCommandHandler(
    IAppDbContext context,
    ILogger<UpdateWorkOrderTasksCommandHandler> logger)
    : IRequestHandler<UpdateWorkOrderTasksCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateWorkOrderTasksCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateWorkOrderTasksCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.ServiceTasks)
            .FirstOrDefaultAsync(w => w.Id == command.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        var distinctTaskIds = command.ServiceTaskIds.Distinct().ToList();

        var tasks = await _context.ServiceTasks
            .Where(t => distinctTaskIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (tasks.Count != distinctTaskIds.Count)
        {
            var missingTaskId = distinctTaskIds
                .First(id => tasks.All(t => t.Id != id));

            return WorkOrderErrors.ServiceTaskMissing(missingTaskId);
        }

        var updateResult = workOrder.UpdateServiceTasks(tasks);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Work order {WorkOrderId} now has {TaskCount} service(s).",
            command.WorkOrderId,
            tasks.Count);

        return Result.Updated;
    }
}
