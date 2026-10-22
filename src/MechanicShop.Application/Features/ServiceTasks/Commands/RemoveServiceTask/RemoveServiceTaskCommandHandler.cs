using MechanicShop.Application.Common.Interfaces;

using MediatR;

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

        // Catalog retire-vs-delete policy lands with work orders (F4): once service
        // tasks are referenced by jobs, hard delete must be blocked or archived.
        _context.ServiceTasks.Remove(serviceTask);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Service task {ServiceTaskId} removed.", command.ServiceTaskId);

        return Result.Deleted;
    }
}
