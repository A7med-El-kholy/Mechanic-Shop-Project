using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderStatus;

public sealed record UpdateWorkOrderStatusCommand(
    Guid WorkOrderId,
    int State)
    : IRequest<Result<Updated>>;
