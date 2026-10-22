using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.UpdateServiceTask;

public sealed class UpdateServiceTaskCommandHandler(
    IAppDbContext context,
    ILogger<UpdateServiceTaskCommandHandler> logger)
    : IRequestHandler<UpdateServiceTaskCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateServiceTaskCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateServiceTaskCommand command, CancellationToken cancellationToken)
    {
        var serviceTask = await _context.ServiceTasks
            .FirstOrDefaultAsync(t => t.Id == command.ServiceTaskId, cancellationToken);

        if (serviceTask is null)
        {
            return ServiceTaskErrors.NotFound(command.ServiceTaskId);
        }

        var normalized = command.Name.Trim().ToLower();

        var nameTakenByOther = await _context.ServiceTasks
            .AnyAsync(t => t.Id != command.ServiceTaskId && t.Name.ToLower() == normalized, cancellationToken);

        if (nameTakenByOther)
        {
            return ServiceTaskErrors.DuplicateName;
        }

        var updateResult = serviceTask.Update(
            command.Name,
            command.BasePrice,
            command.EstimatedDurationInMins);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        // Optional part link: absent -> keep the existing one; present ->
        // validate it, then point the task at it (Guid.Empty clears).
        if (command.PartId is { } partId)
        {
            if (partId == Guid.Empty)
            {
                serviceTask.SetPart(null);
            }
            else
            {
                var partExists = await _context.Parts
                    .AnyAsync(p => p.Id == partId, cancellationToken);

                if (!partExists)
                {
                    return PartErrors.NotFound(partId);
                }

                serviceTask.SetPart(partId);
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsServiceTaskNameUniqueViolation(exception))
        {
            _logger.LogWarning(exception, "Service task update rejected: another task claimed the name.");

            return ServiceTaskErrors.DuplicateName;
        }

        _logger.LogInformation("Service task {ServiceTaskId} updated.", command.ServiceTaskId);

        return Result.Updated;
    }
}
