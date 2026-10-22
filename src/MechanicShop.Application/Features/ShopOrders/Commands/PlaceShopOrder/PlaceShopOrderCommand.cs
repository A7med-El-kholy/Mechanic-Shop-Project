using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using MediatR;

namespace MechanicShop.Application.Features.ShopOrders.Commands.PlaceShopOrder;

public sealed record ShopOrderItemRequest(Guid PartId, int Quantity);

/// <summary>Checkout of the parts cart. Card closes the order immediately; cash
/// reserves it for pickup. Both DEDUCT stock at placement — reservations are
/// real. Validated here, not by the database, so the buyer gets a clear 409.</summary>
public sealed record PlaceShopOrderCommand(List<ShopOrderItemRequest> Items, PaymentMethod Method)
    : IRequest<Result<PartOrderDto>>;
