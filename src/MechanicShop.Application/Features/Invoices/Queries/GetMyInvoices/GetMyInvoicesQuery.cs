using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetMyInvoices;

/// <summary>The customer's billing view: invoices for THEIR cars, resolved from
/// the token — never from a client-supplied customer id.</summary>
public sealed record GetMyInvoicesQuery
    : IRequest<Result<List<InvoiceSummaryDto>>>;
