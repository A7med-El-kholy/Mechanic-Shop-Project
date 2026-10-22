using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Application.Features.Parts.Mappers;
using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.ShopOrders.Queries.GetMyShopOrders;

public sealed class GetMyShopOrdersQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetMyShopOrdersQuery, Result<List<PartOrderDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<List<PartOrderDto>>> Handle(GetMyShopOrdersQuery query, CancellationToken cancellationToken)
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

        var orders = await _context.ShopOrders
            .Include(o => o.Lines)
            .AsNoTracking()
            .Where(o => o.CustomerId == customer.Id)
            .OrderByDescending(o => o.PlacedOn)
            .ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        return orders.ToDtos();
    }
}
