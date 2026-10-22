using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Vehicles.Commands.UpdateVehicle;

public sealed class UpdateVehicleCommandHandler(
    IAppDbContext context,
    ILogger<UpdateVehicleCommandHandler> logger)
    : IRequestHandler<UpdateVehicleCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateVehicleCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleErrors.NotFound(command.VehicleId);
        }

        var vin = command.Vin.Trim().ToUpperInvariant();
        var plate = command.LicensePlate.Trim().ToUpperInvariant();

        // Global uniqueness excluding this vehicle itself, for both VIN and plate.
        var vinTaken = await _context.Vehicles
            .AnyAsync(v => v.Id != command.VehicleId && v.Vin == vin, cancellationToken);

        if (vinTaken)
        {
            return VehicleErrors.DuplicateVin;
        }

        var plateTaken = await _context.Vehicles
            .AnyAsync(v => v.Id != command.VehicleId && v.LicensePlate == plate, cancellationToken);

        if (plateTaken)
        {
            return VehicleErrors.DuplicateLicensePlate;
        }

        var updateResult = vehicle.Update(
            command.Make,
            command.Model,
            command.Year,
            plate,
            vin);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsVehicleVinUniqueViolation(exception))
        {
           _logger.LogWarning(exception, "Vehicle update rejected: another vehicle claimed the VIN.");

            return VehicleErrors.DuplicateVin;
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsVehicleLicensePlateUniqueViolation(exception))
        {
           _logger.LogWarning(exception, "Vehicle update rejected: another vehicle claimed the license plate.");

            return VehicleErrors.DuplicateLicensePlate;
        }

        _logger.LogInformation("Vehicle {VehicleId} updated.", command.VehicleId);

        return Result.Updated;
    }
}
