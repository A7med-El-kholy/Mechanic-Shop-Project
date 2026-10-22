using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Commands.PayInvoice;

/// <summary>The customer's pay action from the portal. Method Card marks the
/// invoice paid immediately (a prototype-only click — no card data exists in
/// this system); Cash means "will pay at the shop" and parks the invoice in
/// CashPending until the manager confirms receipt.</summary>
public sealed record PayInvoiceCommand(Guid InvoiceId, PaymentMethod Method)
    : IRequest<Result<Updated>>;
