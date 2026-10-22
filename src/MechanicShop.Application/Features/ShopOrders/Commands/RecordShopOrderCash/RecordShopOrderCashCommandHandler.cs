using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.ShopOrders.Commands.RecordShopOrderCash;

public sealed class RecordShopOrderCashCommandHandler(IAppDbContext context, ILogger<RecordShopOrderCashCommandHandler> logger)
    : IRequestHandler<RecordShopOrderCashCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RecordShopOrderCashCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(RecordShopOrderCashCommand command, CancellationToken cancellationToken)
    {
        var order = await _context.ShopOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return PartOrderErrors.NotFound(command.OrderId);
        }

        var received = order.RecordCashReceived();

        if (received.IsError)
        {
            return received.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cash received for shop order {OrderNumber}; closed as Paid.",
            order.Number);

        return Result.Updated;
    }
}
