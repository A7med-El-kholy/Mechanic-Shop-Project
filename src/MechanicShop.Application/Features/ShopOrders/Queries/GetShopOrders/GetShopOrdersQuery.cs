using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ShopOrders.Queries.GetShopOrders;

/// <summary>The manager's shop-orders book: everything customers bought, newest first.</summary>
public sealed record GetShopOrdersQuery(int Page = 1, int PageSize = 10)
    : IRequest<Result<PaginatedList<PartOrderDto>>>;
