using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees;

using MediatR;

using MechanicShop.Application.Features.Invoices;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.Inventory;
using MechanicShop.Domain.Notifications;
using MechanicShop.Domain.Notifications.Enums;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderStatus;

public sealed class UpdateWorkOrderStatusCommandHandler(
    IAppDbContext context,
    IUser caller,
    ILogger<UpdateWorkOrderStatusCommandHandler> logger)
    : IRequestHandler<UpdateWorkOrderStatusCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _caller = caller;
    private readonly ILogger<UpdateWorkOrderStatusCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateWorkOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var scope = await StaffScope.ResolveMineAsync(_context, _caller, cancellationToken);

        if (scope.IsError)
        {
            return scope.Errors;
        }

        var mine = scope.Value.TechnicianId;

        var workOrder = await _context.WorkOrders
            .Include(w => w.ServiceTasks)
                .ThenInclude(t => t.Part)
            .Include(w => w.Vehicle)
            .FirstOrDefaultAsync(
                w => w.Id == command.WorkOrderId && (mine == null || w.TechnicianId == mine),
                cancellationToken);

        if (workOrder is null)
        {
            // Same wall as reads: a foreign ticket does not exist for this caller.
            return WorkOrderErrors.NotFound(command.WorkOrderId);
        }

        var before = workOrder.State;
        var target = (WorkOrderState)command.State;

        // The prototype's role map: a technician works their job forward step by
        // step (start, complete); only the counter can route it sideways (cancel,
        // or jump steps). Enforced here because that rule is about hands, not
        // about who owns the HTTP surface.
        if (scope.Value.TechnicianId is not null)
        {
            var stepForward = before == WorkOrderState.Scheduled && target == WorkOrderState.InProgress ||
                              before == WorkOrderState.InProgress && target == WorkOrderState.Completed;

            if (!stepForward)
            {
                return Error.Forbidden(
                    "WorkOrder_TechnicianTransition",
                    "Technicians advance their own job one step at a time (start, complete).");
            }
        }

        var updateResult = workOrder.UpdateState(target);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        var now = DateTimeOffset.UtcNow;
        var vehicle = workOrder.Vehicle!;

        // The prototype's mail(): every transition sends the owner a "Repair
        // update". The state label is the display form ("In Progress"), not
        // the enum name — the stored message is what the customer reads.
        var stateLabel = workOrder.State switch
        {
            WorkOrderState.InProgress => "In Progress",
            WorkOrderState.Completed => "Completed",
            WorkOrderState.Cancelled => "Cancelled",
            _ => workOrder.State.ToString()
        };

        var customerUpdate = Notification.ForCustomer(
            Guid.NewGuid(),
            vehicle.CustomerId,
            "Repair update",
            $"Your vehicle {vehicle.LicensePlate} ({vehicle.Make} {vehicle.Model}) is now {stateLabel}.",
            NotificationCategory.Repairs,
            workOrder.Id,
            now);

        if (customerUpdate.IsError)
        {
            return customerUpdate.Errors;
        }

        _context.Notifications.Add(customerUpdate.Value);

        // Completing a job issues its invoice in the SAME persistence unit: the
        // prototype's rule ("they appear when a repair is completed") without a
        // half-state where the job is done but billed to nobody. Lines snapshot
        // the tasks as charged; later catalog edits stay off the document.
        if (workOrder.State == WorkOrderState.Completed)
        {
            var alreadyInvoiced = await _context.Invoices
                .AnyAsync(i => i.WorkOrderId == workOrder.Id, cancellationToken);

            if (!alreadyInvoiced)
            {
                var invoice = Invoice.Create(
                    Guid.NewGuid(),
                    Invoice.NewNumber(),
                    workOrder.Id,
                    workOrder.ScheduledDate,
                    [.. workOrder.ServiceTasks.Select(t => (t.Id, t.Name, t.BasePrice))],
                    InvoiceTaxDefaults.Rate);

                if (invoice.IsError)
                {
                    return invoice.Errors;
                }

                _context.Invoices.Add(invoice.Value);

                var billed = Notification.ForStaff(
                    Guid.NewGuid(),
                    "Repair completed",
                    $"{vehicle.LicensePlate} was completed. Invoice {invoice.Value.Number} was issued.",
                    NotificationCategory.Billing,
                    workOrder.Id,
                    now);

                if (billed.IsError)
                {
                    return billed.Errors;
                }

                _context.Notifications.Add(billed.Value);

                _logger.LogInformation(
                    "Invoice {InvoiceNumber} issued for completed work order {WorkOrderId}.",
                    invoice.Value.Number,
                    workOrder.Id);
            }

            // The prototype's consumption rule: each completed task eats one
            // unit of its main part, off the same save — job result, invoice
            // and shelf all change together or not at all.
            foreach (var task in workOrder.ServiceTasks)
            {
                if (task.Part is not null)
                {
                    var quantityBefore = task.Part.Quantity;
                    var consumed = task.Part.Adjust(-1);

                    if (consumed.IsError)
                    {
                        return consumed.Errors;
                    }

                    _context.StockMovements.Add(StockMovement.Create(task.Part.Id, -1, quantityBefore,
                        task.Part.Quantity, $"Work order {workOrder.Id}: {task.Name}", _caller.UserId));

                    _logger.LogInformation(
                        "Part {PartId} ({State}) deducted for completed task {TaskId}.",
                        task.Part.Id,
                        task.Part.StockState,
                        task.Id);

                    // The prototype's low-stock alert: every deduction that
                    // leaves the shelf at/below its minimum raises a staff row.
                    if (task.Part.Quantity <= task.Part.MinimumQuantity)
                    {
                        var lowStock = Notification.ForStaff(
                            Guid.NewGuid(),
                            "Low stock",
                            $"{task.Part.Name} is down to {task.Part.Quantity} " +
                            $"(minimum {task.Part.MinimumQuantity}).",
                            NotificationCategory.Inventory,
                            workOrder.Id,
                            now);

                        if (lowStock.IsError)
                        {
                            return lowStock.Errors;
                        }

                        _context.Notifications.Add(lowStock.Value);
                    }
                }
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception,
                "Work order {WorkOrderId} completion conflicted with another stock update.", workOrder.Id);
            return PartErrors.StockChanged;
        }

        _logger.LogInformation(
            "Work order {WorkOrderId} moved to {WorkOrderState}.",
            command.WorkOrderId,
            (WorkOrderState)command.State);

        return Result.Updated;
    }
}
