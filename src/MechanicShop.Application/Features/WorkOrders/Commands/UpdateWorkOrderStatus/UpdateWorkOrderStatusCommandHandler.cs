using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Application.Features.Invoices;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
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
            .Include(w => w.ServiceTasks)
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

                _logger.LogInformation(
                    "Invoice {InvoiceNumber} issued for completed work order {WorkOrderId}.",
                    invoice.Value.Number,
                    workOrder.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Work order {WorkOrderId} moved to {WorkOrderState}.",
            command.WorkOrderId,
            (WorkOrderState)command.State);

        return Result.Updated;
    }
}
