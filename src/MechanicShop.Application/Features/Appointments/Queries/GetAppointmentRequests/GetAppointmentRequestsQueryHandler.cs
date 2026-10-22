using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Appointments.Dtos;
using MechanicShop.Application.Features.Appointments.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Appointments.Queries.GetAppointmentRequests;

public sealed class GetAppointmentRequestsQueryHandler(IAppDbContext context)
    : IRequestHandler<GetAppointmentRequestsQuery, Result<List<AppointmentRequestDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<AppointmentRequestDto>>> Handle(GetAppointmentRequestsQuery query, CancellationToken cancellationToken)
    {
        var requests = await _context.AppointmentRequests
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .Include(r => r.ServiceTask)
            .AsNoTracking()
            // Pending first, then soonest preferred dates; id keeps paging-stable
            // ordering (the same convention as the work-order timeline).
            .OrderBy(r => r.Status)
            .ThenBy(r => r.PreferredDate)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return requests.ToDtos();
    }
}
