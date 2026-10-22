using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Vehicles.Commands.RemoveVehicle;

public sealed class RemoveVehicleCommandHandler(
    IAppDbContext context,
    ILogger<RemoveVehicleCommandHandler> logger)
    : IRequestHandler<RemoveVehicleCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveVehicleCommandHandler> _logger = logger;

    public async Task<Result<Deleted>> Handle(RemoveVehicleCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleErrors.NotFound(command.VehicleId);
        }

        // The prototype blocks removing vehicles with active or completed work orders
        // (archive instead). That guard lands with the work-order feature.
        _context.Vehicles.Remove(vehicle);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle {VehicleId} removed.", command.VehicleId);

        return Result.Deleted;
    }
}
