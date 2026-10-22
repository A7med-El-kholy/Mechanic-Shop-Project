using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoices;

/// <summary>The billing book: every invoice the shop has issued, newest first.</summary>
public sealed record GetInvoicesQuery(int Page = 1, int PageSize = 10)
    : IRequest<Result<PaginatedList<InvoiceSummaryDto>>>;
