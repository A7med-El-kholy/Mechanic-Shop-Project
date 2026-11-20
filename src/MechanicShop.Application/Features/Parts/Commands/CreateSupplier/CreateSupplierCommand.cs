using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Parts.Commands.CreateSupplier;

public sealed record CreateSupplierCommand(string Name, string? ContactName, string? Email, string? Phone)
    : IRequest<Result<SupplierDto>>;
