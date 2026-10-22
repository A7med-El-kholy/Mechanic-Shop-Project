using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Application.Features.ServiceTasks.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.CreateServiceTask;

public sealed class CreateServiceTaskCommandHandler(
    IAppDbContext context,
    ILogger<CreateServiceTaskCommandHandler> logger)
    : IRequestHandler<CreateServiceTaskCommand, Result<ServiceTaskDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateServiceTaskCommandHandler> _logger = logger;

    public async Task<Result<ServiceTaskDto>> Handle(CreateServiceTaskCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        var normalized = name.ToLower();

        // Same-name services would be indistinguishable in booking dropdowns,
        // so the catalog name is globally unique (checked here for a friendly
        // response, and by a unique index for concurrent inserts).
        var nameTaken = await _context.ServiceTasks
            .AnyAsync(t => t.Name.ToLower() == normalized, cancellationToken);

        if (nameTaken)
        {
            return ServiceTaskErrors.DuplicateName;
        }

        var taskResult = ServiceTask.Create(
            Guid.NewGuid(),
            name,
            command.BasePrice,
            command.EstimatedDurationInMins);

        if (taskResult.IsError)
        {
            return taskResult.Errors;
        }

        _context.ServiceTasks.Add(taskResult.Value);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsServiceTaskNameUniqueViolation(exception))
        {
            _logger.LogWarning(exception, "Service task create rejected: a racing insert claimed the name.");

            return ServiceTaskErrors.DuplicateName;
        }

        _logger.LogInformation("Service task {ServiceTaskId} created.", taskResult.Value.Id);

        return taskResult.Value.ToDto();
    }
}
