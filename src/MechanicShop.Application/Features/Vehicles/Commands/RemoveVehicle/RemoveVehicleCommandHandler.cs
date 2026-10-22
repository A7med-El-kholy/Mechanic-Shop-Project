using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.WorkOrders.Enums;

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

        // Prototype rule, first gate: active jobs (scheduled or in progress) block
        // removal entirely. Statements are spelled out because the entity's computed
        // IsActive property cannot be translated to SQL.
        var hasActiveJobs = await _context.WorkOrders
            .AnyAsync(w =>
                w.VehicleId == command.VehicleId &&
                (w.State == WorkOrderState.Scheduled || w.State == WorkOrderState.InProgress),
                cancellationToken);

        if (hasActiveJobs)
        {
            _logger.LogWarning(
                "Vehicle {VehicleId} removal blocked: active work orders exist.",
                command.VehicleId);

            return VehicleErrors.HasActiveWorkOrders;
        }

        var hasHistory = await _context.WorkOrders
            .AnyAsync(w => w.VehicleId == command.VehicleId, cancellationToken);

        if (hasHistory)
        {
            // Prototype rule, second gate: history means the vehicle is archived,
            // not deleted — it leaves lists and new bookings, restorable any time.
            vehicle.Archive();

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Vehicle {VehicleId} archived (has service history) instead of deleted.",
                command.VehicleId);

            return Result.Deleted;
        }

        _context.Vehicles.Remove(vehicle);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle {VehicleId} removed.", command.VehicleId);

        return Result.Deleted;
    }
}
