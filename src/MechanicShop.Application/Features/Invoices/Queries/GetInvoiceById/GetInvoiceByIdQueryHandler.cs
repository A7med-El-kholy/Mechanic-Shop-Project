using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Application.Features.Invoices.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery query, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(invoice => invoice.WorkOrder)
                .ThenInclude(w => w!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .Include(invoice => invoice.LineItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(invoice => invoice.Id == query.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return InvoiceErrors.NotFound(query.InvoiceId);
        }

        return invoice.ToDto();
    }
}
