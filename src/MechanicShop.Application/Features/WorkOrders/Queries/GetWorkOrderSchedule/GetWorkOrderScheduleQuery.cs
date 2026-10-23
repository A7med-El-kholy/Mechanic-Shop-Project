using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderSchedule;

/// <summary>
/// One day's board: every live job, ordered by technician and start time.
/// Cancelled work is history, not schedule, so it never reaches the board.
/// The optional technician narrows the board to one lane.
/// </summary>
public sealed record GetWorkOrderScheduleQuery(
    DateOnly Date,
    Guid? TechnicianId = null)
    : IRequest<Result<List<Dtos.ScheduleJobDto>>>;
