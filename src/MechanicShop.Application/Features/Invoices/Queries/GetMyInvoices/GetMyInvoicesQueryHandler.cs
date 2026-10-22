using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Appointments;
using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Application.Features.Invoices.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Invoices.Queries.GetMyInvoices;

public sealed class GetMyInvoicesQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetMyInvoicesQuery, Result<List<InvoiceSummaryDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<List<InvoiceSummaryDto>>> Handle(GetMyInvoicesQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        // Same deliberate workshop-first rule: an account without a linked
        // profile simply has no invoices (the portal wording stays identical).
        if (customer is null)
        {
            return AppointmentRequestErrors.NoProfileLinked;
        }

        var invoices = await _context.Invoices
            .Include(invoice => invoice.WorkOrder)
                .ThenInclude(w => w!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .AsNoTracking()
            .Where(invoice => invoice.WorkOrder!.Vehicle!.CustomerId == customer.Id)
            .OrderByDescending(invoice => invoice.IssuedOn)
            .ThenByDescending(invoice => invoice.Id)
            .ToListAsync(cancellationToken);

        return invoices.ToSummaryDtos();
    }
}
