using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.Queries.GetMyCustomer;

public sealed class GetMyCustomerQueryHandler(
    IAppDbContext context,
    IUser user)
    : IRequestHandler<GetMyCustomerQuery, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<CustomerDto>> Handle(GetMyCustomerQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var customer = await _context.Customers
            .Include(c => c.Vehicles)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            // Design decision: profiles are created by the workshop. A signed-up user
            // with no profile gets an explicit "not linked" answer rather than a stub.
            return Error.NotFound(
                "Customer_ProfileNotLinked",
                "No customer profile is linked to this account yet. Profiles are created by the workshop using the same email.");
        }

        return customer.ToDto();
    }
}
