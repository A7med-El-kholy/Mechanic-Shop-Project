using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Include(c => c.Vehicles.Where(v => !v.IsArchived))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.CustomerId, cancellationToken);

        if (customer is null)
        {
            return CustomerErrors.NotFound(query.CustomerId);
        }

        return customer.ToDto();
    }
}
