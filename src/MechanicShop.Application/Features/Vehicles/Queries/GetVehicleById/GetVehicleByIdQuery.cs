using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Vehicles.Queries.GetVehicleById;

public sealed record GetVehicleByIdQuery(Guid VehicleId)
    : IRequest<Result<VehicleDto>>;
