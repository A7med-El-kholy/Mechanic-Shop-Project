using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;

using MediatR;

namespace MechanicShop.Application.Features.Parts.Commands.CreatePart;

public sealed record CreatePartCommand(
    string Name,
    int Category,
    decimal Price,
    int Quantity,
    int MinimumQuantity)
    : IRequest<Result<PartDto>>;
