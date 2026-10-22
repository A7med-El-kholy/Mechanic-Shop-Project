using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Vehicles.Commands.UpdateVehicle;

public sealed record UpdateVehicleCommand(
    Guid VehicleId,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Vin)
    : IRequest<Result<Updated>>;
