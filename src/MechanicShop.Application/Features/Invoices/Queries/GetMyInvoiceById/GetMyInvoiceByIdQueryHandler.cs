using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Appointments;
using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Application.Features.Invoices.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Invoices.Queries.GetMyInvoiceById;

public sealed class GetMyInvoiceByIdQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetMyInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<InvoiceDto>> Handle(GetMyInvoiceByIdQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            return AppointmentRequestErrors.NoProfileLinked;
        }

        // Someone else's invoice number must be indistinguishable from a wrong
        // guess: both read not-found.
        var invoice = await _context.Invoices
            .Include(invoice => invoice.WorkOrder)
                .ThenInclude(w => w!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .Include(invoice => invoice.LineItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                invoice => invoice.Id == query.InvoiceId &&
                           invoice.WorkOrder!.Vehicle!.CustomerId == customer.Id,
                cancellationToken);

        if (invoice is null)
        {
            return InvoiceErrors.NotFound(query.InvoiceId);
        }

        return invoice.ToDto();
    }
}
