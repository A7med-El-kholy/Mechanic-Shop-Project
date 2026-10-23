using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Notifications;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Commands.RecordCashPayment;

public sealed class RecordCashPaymentCommandHandler(IAppDbContext context, ILogger<RecordCashPaymentCommandHandler> logger)
    : IRequestHandler<RecordCashPaymentCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RecordCashPaymentCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(RecordCashPaymentCommand command, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(invoice => invoice.WorkOrder)
                .ThenInclude(w => w!.Vehicle)
            .FirstOrDefaultAsync(invoice => invoice.Id == command.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return InvoiceErrors.NotFound(command.InvoiceId);
        }

        var received = invoice.Pay(PaymentMethod.Cash);

        if (received.IsError)
        {
            return received.Errors;
        }

        var customerId = invoice.WorkOrder!.Vehicle!.CustomerId;

        var receipt = Notification.ForCustomer(
            Guid.NewGuid(),
            customerId,
            "Payment received",
            $"Invoice {invoice.Number} is paid in cash.",
            NotificationCategory.Billing,
            invoice.WorkOrderId,
            DateTimeOffset.UtcNow);

        if (receipt.IsError)
        {
            return receipt.Errors;
        }

        _context.Notifications.Add(receipt.Value);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cash received for invoice {InvoiceId}; closed as Paid.",
            invoice.Id);

        return Result.Updated;
    }
}
