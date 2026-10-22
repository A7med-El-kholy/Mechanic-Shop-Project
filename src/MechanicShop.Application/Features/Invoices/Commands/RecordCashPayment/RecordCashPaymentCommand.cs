using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Commands.RecordCashPayment;

/// <summary>The manager's "Record cash payment" from the prototype: the money
/// physically arrived at the counter, so the invoice closes. Allowed from
/// Unpaid AND CashPending (the customer may also simply pay cash on the spot).</summary>
public sealed record RecordCashPaymentCommand(Guid InvoiceId)
    : IRequest<Result<Updated>>;
