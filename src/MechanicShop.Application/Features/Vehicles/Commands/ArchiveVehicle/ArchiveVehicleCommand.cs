using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Vehicles.Commands.ArchiveVehicle;

public sealed record ArchiveVehicleCommand(Guid VehicleId)
    : IRequest<Result<Updated>>;
