using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.RemoveServiceTask;

public sealed class RemoveServiceTaskCommandHandler(
    IAppDbContext context,
    ILogger<RemoveServiceTaskCommandHandler> logger)
    : IRequestHandler<RemoveServiceTaskCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveServiceTaskCommandHandler> _logger = logger;

    public async Task<Result<Deleted>> Handle(RemoveServiceTaskCommand command, CancellationToken cancellationToken)
    {
        var serviceTask = await _context.ServiceTasks
            .FirstOrDefaultAsync(t => t.Id == command.ServiceTaskId, cancellationToken);

        if (serviceTask is null)
        {
            return ServiceTaskErrors.NotFound(command.ServiceTaskId);
        }

        var inUse = await _context.WorkOrders
            .AnyAsync(w => w.ServiceTasks.Any(t => t.Id == command.ServiceTaskId), cancellationToken);

        if (inUse)
        {
            _logger.LogWarning(
                "Service task {ServiceTaskId} removal blocked: referenced by work orders.",
                command.ServiceTaskId);

            return ServiceTaskErrors.InUseByWorkOrders;
        }

        // Pending customer requests cite the catalog too; deleting the task would
        // strand the request's meaning.
        var requested = await _context.AppointmentRequests
            .AnyAsync(r => r.ServiceTaskId == command.ServiceTaskId, cancellationToken);

        if (requested)
        {
            _logger.LogWarning(
                "Service task {ServiceTaskId} removal blocked: cited on appointment requests.",
                command.ServiceTaskId);

            return ServiceTaskErrors.InUseByAppointmentRequests;
        }

        _context.ServiceTasks.Remove(serviceTask);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Service task {ServiceTaskId} removed.", command.ServiceTaskId);

        return Result.Deleted;
    }
}
