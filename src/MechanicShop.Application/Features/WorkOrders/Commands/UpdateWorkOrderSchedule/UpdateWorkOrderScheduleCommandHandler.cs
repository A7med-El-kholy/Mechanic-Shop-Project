using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderSchedule;

/// <summary>
/// Moves an open booking: new day, slot, bay and/or technician. Same rules as
/// opening a work order, incl. the prototype's conf() — with the moving order
/// excluded from its own conflict scan.
/// </summary>
public sealed class UpdateWorkOrderScheduleCommandHandler(
    IAppDbContext context,
    ILogger<UpdateWorkOrderScheduleCommandHandler> logger)
    : IRequestHandler<UpdateWorkOrderScheduleCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateWorkOrderScheduleCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateWorkOrderScheduleCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .FirstOrDefaultAsync(w => w.Id == command.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        var technicianExists = await _context.Employees
            .AnyAsync(e => e.Id == command.TechnicianId, cancellationToken);

        if (!technicianExists)
        {
            return EmployeeErrors.NotFound(command.TechnicianId);
        }

        var rescheduleResult = workOrder.Reschedule(
            command.ScheduledDate,
            command.StartsAtMinute,
            (Spot)command.Spot,
            command.TechnicianId);

        if (rescheduleResult.IsError)
        {
            return rescheduleResult.Errors;
        }

        var sameDayActiveJobs = await _context.WorkOrders
            .Include(w => w.Technician)
            .Include(w => w.ServiceTasks)
            .Where(w =>
                w.Id != command.WorkOrderId &&
                w.ScheduledDate == command.ScheduledDate &&
                (w.State == WorkOrderState.Scheduled || w.State == WorkOrderState.InProgress))
            .ToListAsync(cancellationToken);

        var conflicts = workOrder.FindTimeConflicts(sameDayActiveJobs);

        if (conflicts.Count > 0)
        {
            _logger.LogWarning(
                "Work order {WorkOrderId} reschedule rejected: scheduling conflicts on {ScheduledDate} at {StartsAtMinute}.",
                command.WorkOrderId,
                command.ScheduledDate,
                command.StartsAtMinute);

            return WorkOrderErrors.SchedulingConflict(string.Join(" ", conflicts));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Work order {WorkOrderId} rescheduled to {ScheduledDate} at {StartsAtMinute} in bay {Spot}.",
            command.WorkOrderId,
            command.ScheduledDate,
            command.StartsAtMinute,
            (Spot)command.Spot);

        return Result.Updated;
    }
}
