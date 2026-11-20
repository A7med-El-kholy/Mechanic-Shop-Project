using MechanicShop.Application.Features.Parts.Dtos;
using MediatR;

namespace MechanicShop.Application.Features.Parts.Queries.GetPurchaseOrders;

public sealed record GetPurchaseOrdersQuery : IRequest<List<PurchaseOrderDto>>;
