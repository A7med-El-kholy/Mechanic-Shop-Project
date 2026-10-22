using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ShopOrders.Queries.GetMyShopOrders;

/// <summary>The customer's own orders, resolved from the token.</summary>
public sealed record GetMyShopOrdersQuery
    : IRequest<Result<List<PartOrderDto>>>;
