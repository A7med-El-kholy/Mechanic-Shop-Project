using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Parts.Queries.GetStockMovements;

public sealed record GetStockMovementsQuery(Guid PartId) : IRequest<Result<List<StockMovementDto>>>;
