using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;

public sealed record RemoveWorkOrderCommand(Guid WorkOrderId)
    : IRequest<Result<Deleted>>;
