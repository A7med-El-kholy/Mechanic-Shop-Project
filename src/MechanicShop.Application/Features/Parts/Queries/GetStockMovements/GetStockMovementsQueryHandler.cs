using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Parts.Queries.GetStockMovements;

public sealed class GetStockMovementsQueryHandler(IAppDbContext context)
    : IRequestHandler<GetStockMovementsQuery, Result<List<StockMovementDto>>>
{
    public async Task<Result<List<StockMovementDto>>> Handle(GetStockMovementsQuery query, CancellationToken cancellationToken)
    {
        if (!await context.Parts.AnyAsync(part => part.Id == query.PartId, cancellationToken))
            return PartErrors.NotFound(query.PartId);

        var movements = await context.StockMovements.AsNoTracking()
            .Where(movement => movement.PartId == query.PartId)
            .OrderByDescending(movement => movement.RecordedAt)
            .Take(100)
            .Select(movement => new StockMovementDto(movement.Id, movement.PartId, movement.Delta,
                movement.QuantityBefore, movement.QuantityAfter, movement.Reason, movement.ActorUserId,
                movement.RecordedAt))
            .ToListAsync(cancellationToken);

        return movements;
    }
}
