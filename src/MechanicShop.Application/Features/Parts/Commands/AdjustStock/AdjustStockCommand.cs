using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Parts.Commands.AdjustStock;

/// <summary>The storeroom's −1 / +1 / Restock buttons from the prototype:
/// one signed delta against one shelf.</summary>
public sealed record AdjustStockCommand(Guid PartId, int Delta, string Reason, string? ActorUserId)
    : IRequest<Result<Updated>>;
