using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Parts.Commands.CreatePurchaseOrder;

public sealed record PurchaseOrderItemRequest(Guid PartId, int Quantity, decimal UnitCost);

public sealed record CreatePurchaseOrderCommand(Guid SupplierId, List<PurchaseOrderItemRequest> Items)
    : IRequest<Result<PurchaseOrderDto>>;
