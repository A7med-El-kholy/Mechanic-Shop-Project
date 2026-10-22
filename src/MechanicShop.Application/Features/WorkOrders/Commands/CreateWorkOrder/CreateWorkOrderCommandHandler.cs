using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(
    IAppDbContext context,
    ILogger<CreateWorkOrderCommandHandler> logger)
    : IRequestHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateWorkOrderCommandHandler> _logger = logger;

    public async Task<Result<WorkOrderDto>> Handle(CreateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var vehicleExists = await _context.Vehicles
            .AnyAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (!vehicleExists)
        {
            return WorkOrderErrors.VehicleMissing(command.VehicleId);
        }

        var vehicleArchived = await _context.Vehicles
            .AnyAsync(v => v.Id == command.VehicleId && v.IsArchived, cancellationToken);

        if (vehicleArchived)
        {
            // Archived vehicles leave "new bookings" per the prototype.
            return WorkOrderErrors.VehicleArchived(command.VehicleId);
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

        var workOrderResult = WorkOrder.Create(Guid.NewGuid(), command.VehicleId, tasks);

        if (workOrderResult.IsError)
        {
            return workOrderResult.Errors;
        }

        _context.WorkOrders.Add(workOrderResult.Value);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Work order {WorkOrderId} created for vehicle {VehicleId} with {TaskCount} service(s).",
            workOrderResult.Value.Id,
            command.VehicleId,
            tasks.Count);

        var dto = await _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .AsNoTracking()
            .FirstAsync(w => w.Id == workOrderResult.Value.Id, cancellationToken);

        return dto.ToDto();
    }
}
