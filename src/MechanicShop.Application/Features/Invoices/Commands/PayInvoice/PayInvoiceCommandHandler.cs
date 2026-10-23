using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Notifications;
using MechanicShop.Domain.Appointments;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.Notifications;
using MechanicShop.Domain.Notifications.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Commands.PayInvoice;

public sealed class PayInvoiceCommandHandler(IAppDbContext context, IUser user, ILogger<PayInvoiceCommandHandler> logger)
    : IRequestHandler<PayInvoiceCommand, Result<Updated>>
{
    private static readonly List<PaymentMethod> ValidMethods = [PaymentMethod.Card, PaymentMethod.Cash];

    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;
    private readonly ILogger<PayInvoiceCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(PayInvoiceCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        if (!ValidMethods.Contains(command.Method))
        {
            return InvoiceErrors.MethodInvalid;
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            return AppointmentRequestErrors.NoProfileLinked;
        }

        var invoice = await _context.Invoices
            .Include(invoice => invoice.WorkOrder)
                .ThenInclude(w => w!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .FirstOrDefaultAsync(
                invoice => invoice.Id == command.InvoiceId &&
                           invoice.WorkOrder!.Vehicle!.CustomerId == customer.Id,
                cancellationToken);

        // Paying another customer's invoice is the same as paying a wrong
        // number: not found (no existence leak).
        if (invoice is null)
        {
            return InvoiceErrors.NotFound(command.InvoiceId);
        }

        var paid = command.Method == PaymentMethod.Card
            ? invoice.Pay(PaymentMethod.Card)
            : invoice.SelectCashAtShop();

        if (paid.IsError)
        {
            return paid.Errors;
        }

        var now = DateTimeOffset.UtcNow;

        if (command.Method == PaymentMethod.Card)
        {
            // Prototype: paying by card notifies the customer (receipt) and the
            // staff inbox (money arrived) — inside the same save as the state move.
            var receipt = Notification.ForCustomer(
                Guid.NewGuid(),
                customer.Id,
                "Payment received",
                $"Invoice {invoice.Number} ({NotificationFormat.Money(invoice.TotalAmount)}) is paid by card.",
                NotificationCategory.Billing,
                invoice.WorkOrderId,
                now);

            if (receipt.IsError)
            {
                return receipt.Errors;
            }

            _context.Notifications.Add(receipt.Value);

            var staffPaid = Notification.ForStaff(
                Guid.NewGuid(),
                "Invoice paid",
                $"Invoice {invoice.Number} from {customer.Name} was paid by card.",
                NotificationCategory.Billing,
                invoice.WorkOrderId,
                now);

            if (staffPaid.IsError)
            {
                return staffPaid.Errors;
            }

            _context.Notifications.Add(staffPaid.Value);
        }
        else
        {
            // Cash chosen: only the workshop needs to know — the customer
            // already sees the invoice flip to Cash Pending.
            var cashChosen = Notification.ForStaff(
                Guid.NewGuid(),
                "Cash payment chosen",
                $"{customer.Name} will pay invoice {invoice.Number} in cash at the shop.",
                NotificationCategory.Billing,
                invoice.WorkOrderId,
                now);

            if (cashChosen.IsError)
            {
                return cashChosen.Errors;
            }

            _context.Notifications.Add(cashChosen.Value);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Invoice {InvoiceId} moved to {InvoiceStatus} by customer {CustomerId}.",
            invoice.Id,
            invoice.Status,
            customer.Id);

        return Result.Updated;
    }
}
