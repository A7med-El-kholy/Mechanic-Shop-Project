using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Appointments.Dtos;
using MechanicShop.Application.Features.Appointments.Mappers;

using MediatR;

using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Appointments.Queries.GetMyAppointmentRequests;

public sealed class GetMyAppointmentRequestsQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetMyAppointmentRequestsQuery, Result<List<AppointmentRequestDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<List<AppointmentRequestDto>>> Handle(GetMyAppointmentRequestsQuery query, CancellationToken cancellationToken)
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

        var requests = await _context.AppointmentRequests
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .Include(r => r.ServiceTask)
            .AsNoTracking()
            .Where(r => r.CustomerId == customer.Id)
            .OrderByDescending(r => r.PreferredDate)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return requests.ToDtos();
    }
}
