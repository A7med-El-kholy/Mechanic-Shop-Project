using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Application.Features.Invoices.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoices;

public sealed class GetInvoicesQueryHandler(IAppDbContext context)
    : IRequestHandler<GetInvoicesQuery, Result<PaginatedList<InvoiceSummaryDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<InvoiceSummaryDto>>> Handle(GetInvoicesQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var invoicesQuery = _context.Invoices
            .Include(invoice => invoice.WorkOrder)
                .ThenInclude(w => w!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .AsNoTracking();

        if (query.WorkOrderId is { } workOrderId)
        {
            invoicesQuery = invoicesQuery.Where(invoice => invoice.WorkOrderId == workOrderId);
        }

        var count = await invoicesQuery.CountAsync(cancellationToken);

        var items = await invoicesQuery
            .OrderByDescending(invoice => invoice.IssuedOn)
            .ThenByDescending(invoice => invoice.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<InvoiceSummaryDto>(
            items.ToSummaryDtos(),
            page,
            pageSize,
            count);
    }
}
