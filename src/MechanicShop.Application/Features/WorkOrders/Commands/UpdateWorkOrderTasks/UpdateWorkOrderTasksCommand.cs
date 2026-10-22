using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderTasks;

public sealed record UpdateWorkOrderTasksCommand(
    Guid WorkOrderId,
    List<Guid> ServiceTaskIds)
    : IRequest<Result<Updated>>;
