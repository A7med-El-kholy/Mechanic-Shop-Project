using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Parts.Commands.AdjustStock;

public sealed class AdjustStockCommandHandler(IAppDbContext context, ILogger<AdjustStockCommandHandler> logger)
    : IRequestHandler<AdjustStockCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<AdjustStockCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        var part = await _context.Parts
            .FirstOrDefaultAsync(p => p.Id == command.PartId, cancellationToken);

        if (part is null)
        {
            return PartErrors.NotFound(command.PartId);
        }

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 200)
        {
            return PartErrors.MovementReasonRequired;
        }

        var before = part.StockState;
        var quantityBefore = part.Quantity;

        var adjusted = part.Adjust(command.Delta);

        if (adjusted.IsError)
        {
            return adjusted.Errors;
        }

        _context.StockMovements.Add(StockMovement.Create(part.Id, command.Delta, quantityBefore,
            part.Quantity, command.Reason, command.ActorUserId));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Concurrent stock update rejected for part {PartId}.", part.Id);
            return PartErrors.StockChanged;
        }

        _logger.LogInformation(
            "Part {PartId} stock adjusted by {Delta}: {Before} -> {After} ({Quantity} left).",
            part.Id,
            command.Delta,
            before,
            part.StockState,
            part.Quantity);

        return Result.Updated;
    }
}
