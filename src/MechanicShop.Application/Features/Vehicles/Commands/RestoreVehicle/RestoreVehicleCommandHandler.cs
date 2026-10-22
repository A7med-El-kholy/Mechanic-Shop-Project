using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Vehicles.Commands.RestoreVehicle;

public sealed class RestoreVehicleCommandHandler(
    IAppDbContext context,
    ILogger<RestoreVehicleCommandHandler> logger)
    : IRequestHandler<RestoreVehicleCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RestoreVehicleCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(RestoreVehicleCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleErrors.NotFound(command.VehicleId);
        }

        if (!vehicle.IsArchived)
        {
            return VehicleErrors.NotArchived;
        }

        vehicle.Restore();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle {VehicleId} restored.", command.VehicleId);

        return Result.Updated;
    }
}
