using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Vehicles.Commands.RestoreVehicle;

public sealed record RestoreVehicleCommand(Guid VehicleId)
    : IRequest<Result<Updated>>;
