using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Notifications;
using MechanicShop.Domain.Notifications.Enums;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

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
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return WorkOrderErrors.VehicleMissing(command.VehicleId);
        }

        if (vehicle.IsArchived)
        {
            // Archived vehicles leave "new bookings" per the prototype.
            return WorkOrderErrors.VehicleArchived(command.VehicleId);
        }

        var technician = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == command.TechnicianId, cancellationToken);

        if (technician is null)
        {
            return EmployeeErrors.NotFound(command.TechnicianId);
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

        var workOrderResult = WorkOrder.Create(
            Guid.NewGuid(),
            command.VehicleId,
            tasks,
            command.ScheduledDate,
            command.StartsAtMinute,
            (Spot)command.Spot,
            command.TechnicianId);

        if (workOrderResult.IsError)
        {
            return workOrderResult.Errors;
        }

        // Prototype conf(): the new booking may not overlap any live job on its
        // day, by bay or by technician. Historic states can't conflict.
        var sameDayActiveJobs = await _context.WorkOrders
            .Include(w => w.Technician)
            .Include(w => w.ServiceTasks)
            .Where(w =>
                w.ScheduledDate == command.ScheduledDate &&
                (w.State == WorkOrderState.Scheduled || w.State == WorkOrderState.InProgress))
            .ToListAsync(cancellationToken);

        var conflicts = workOrderResult.Value.FindTimeConflicts(sameDayActiveJobs);

        if (conflicts.Count > 0)
        {
            _logger.LogWarning(
                "Work order create rejected: scheduling conflicts on {ScheduledDate} at {StartsAtMinute}.",
                command.ScheduledDate,
                command.StartsAtMinute);

            return WorkOrderErrors.SchedulingConflict(string.Join(" ", conflicts));
        }

        _context.WorkOrders.Add(workOrderResult.Value);

        // The prototype pushes a staff "New assignment" the moment a booking
        // exists — same save, so no scheduling exists without its notification.
        var assignment = Notification.ForStaff(
            Guid.NewGuid(),
            "New assignment",
            $"{vehicle.LicensePlate} ({vehicle.Make} {vehicle.Model}) scheduled for " +
            $"{technician.FullName} at {WorkDay.FormatMinute(command.StartsAtMinute)} " +
            $"in bay {(Spot)command.Spot}.",
            NotificationCategory.Assignments,
            workOrderResult.Value.Id,
            DateTimeOffset.UtcNow);

        if (assignment.IsError)
        {
            return assignment.Errors;
        }

        _context.Notifications.Add(assignment.Value);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Work order {WorkOrderId} scheduled on {ScheduledDate} at {StartsAtMinute} in bay {Spot} with {TaskCount} service(s).",
            workOrderResult.Value.Id,
            command.ScheduledDate,
            command.StartsAtMinute,
            (Spot)command.Spot,
            tasks.Count);

        var dto = await _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .Include(w => w.Technician)
            .AsNoTracking()
            .FirstAsync(w => w.Id == workOrderResult.Value.Id, cancellationToken);

        return dto.ToDto();
    }
}
