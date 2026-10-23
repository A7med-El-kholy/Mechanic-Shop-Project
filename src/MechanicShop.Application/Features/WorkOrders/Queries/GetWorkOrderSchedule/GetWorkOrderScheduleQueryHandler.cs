using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Dtos;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders.Enums;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderSchedule;

public sealed class GetWorkOrderScheduleQueryHandler(IAppDbContext context)
    : IRequestHandler<GetWorkOrderScheduleQuery, Result<List<ScheduleJobDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<ScheduleJobDto>>> Handle(
        GetWorkOrderScheduleQuery query,
        CancellationToken cancellationToken)
    {
        // A board is one bounded day, so loading the entities (instead of a
        // projection) lets EndsAtMinute keep its domain rule — a job is never
        // shorter than one grid slot — without duplicating the arithmetic here.
        var workOrders = await _context.WorkOrders
            .Include(w => w.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(w => w.ServiceTasks)
            .Include(w => w.Technician)
            .AsNoTracking()
            .Where(w => w.ScheduledDate == query.Date &&
                        w.State != WorkOrderState.Cancelled &&
                        (query.TechnicianId == null || w.TechnicianId == query.TechnicianId))
            .OrderBy(w => w.Technician!.FullName)
            .ThenBy(w => w.StartsAtMinute)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);

        var jobs = workOrders
            .Select(w => new ScheduleJobDto(
                w.Id,
                w.Vehicle?.VehicleInfo ?? string.Empty,
                w.Vehicle?.LicensePlate ?? string.Empty,
                w.Vehicle?.Customer?.Name ?? string.Empty,
                w.TechnicianId,
                w.Technician?.FullName ?? string.Empty,
                (int)w.State,
                w.StartsAtMinute,
                w.EndsAtMinute,
                (int)w.Spot))
            .ToList();

        return jobs;
    }
}
