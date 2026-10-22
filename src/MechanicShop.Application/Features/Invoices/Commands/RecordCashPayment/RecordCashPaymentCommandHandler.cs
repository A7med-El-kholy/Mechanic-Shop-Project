using MechanicShop.Application.Common.Interfaces;

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

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cash received for invoice {InvoiceId}; closed as Paid.",
            invoice.Id);

        return Result.Updated;
    }
}
