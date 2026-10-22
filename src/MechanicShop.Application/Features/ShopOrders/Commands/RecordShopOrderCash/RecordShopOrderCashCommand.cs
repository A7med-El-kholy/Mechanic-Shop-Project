using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ShopOrders.Commands.RecordShopOrderCash;

public sealed record RecordShopOrderCashCommand(Guid OrderId)
    : IRequest<Result<Updated>>;
