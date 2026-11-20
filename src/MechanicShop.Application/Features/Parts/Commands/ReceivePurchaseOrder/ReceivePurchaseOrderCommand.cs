using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Parts.Commands.ReceivePurchaseOrder;

public sealed record ReceivePurchaseOrderCommand(Guid PurchaseOrderId, string? ActorUserId)
    : IRequest<Result<PurchaseOrderDto>>;
