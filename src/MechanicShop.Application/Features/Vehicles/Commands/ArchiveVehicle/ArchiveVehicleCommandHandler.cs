using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.WorkOrders.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Vehicles.Commands.ArchiveVehicle;

/// <summary>
/// Manual archiving (the prototype's manager action), independent of the automatic
/// archive that a delete-with-history triggers. Blocked while jobs are active —
/// removing a working vehicle from lists would hide an open commitment.
/// </summary>
public sealed class ArchiveVehicleCommandHandler(
    IAppDbContext context,
    ILogger<ArchiveVehicleCommandHandler> logger)
    : IRequestHandler<ArchiveVehicleCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<ArchiveVehicleCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(ArchiveVehicleCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleErrors.NotFound(command.VehicleId);
        }

        var hasActiveJobs = await _context.WorkOrders
            .AnyAsync(w =>
                w.VehicleId == command.VehicleId &&
                (w.State == WorkOrderState.Scheduled || w.State == WorkOrderState.InProgress),
                cancellationToken);

        if (hasActiveJobs)
        {
            return VehicleErrors.HasActiveWorkOrders;
        }

        if (vehicle.IsArchived)
        {
            return VehicleErrors.AlreadyArchived;
        }

        vehicle.Archive();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle {VehicleId} archived.", command.VehicleId);

        return Result.Updated;
    }
}
