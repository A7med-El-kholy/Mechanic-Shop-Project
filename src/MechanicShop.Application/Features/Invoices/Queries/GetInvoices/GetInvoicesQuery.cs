using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoices;

/// <summary>The billing book: every invoice the shop has issued, newest first.
/// A work order id narrows it to the one ticket that may have produced an
/// invoice — completion issues at most one.</summary>
public sealed record GetInvoicesQuery(int Page = 1, int PageSize = 10, Guid? WorkOrderId = null)
    : IRequest<Result<PaginatedList<InvoiceSummaryDto>>>;
