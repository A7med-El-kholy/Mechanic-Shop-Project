using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderSchedule;

public sealed record UpdateWorkOrderScheduleCommand(
    Guid WorkOrderId,
    DateOnly ScheduledDate,
    int StartsAtMinute,
    int Spot,
    Guid TechnicianId)
    : IRequest<Result<Updated>>;
