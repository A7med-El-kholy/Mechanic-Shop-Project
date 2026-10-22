using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(
    Guid VehicleId,
    List<Guid> ServiceTaskIds,
    DateOnly ScheduledDate,
    int StartsAtMinute,
    int Spot,
    Guid TechnicianId)
    : IRequest<Result<WorkOrderDto>>;
