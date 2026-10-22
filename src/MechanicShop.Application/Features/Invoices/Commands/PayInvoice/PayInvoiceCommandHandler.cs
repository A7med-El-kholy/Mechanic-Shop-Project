using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Appointments;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

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

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Invoice {InvoiceId} moved to {InvoiceStatus} by customer {CustomerId}.",
            invoice.Id,
            invoice.Status,
            customer.Id);

        return Result.Updated;
    }
}
