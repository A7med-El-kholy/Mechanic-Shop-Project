using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicleToCustomer;

public sealed class AddVehicleToCustomerCommandHandler(
    IAppDbContext context,
    ILogger<AddVehicleToCustomerCommandHandler> logger)
    : IRequestHandler<AddVehicleToCustomerCommand, Result<VehicleDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<AddVehicleToCustomerCommandHandler> _logger = logger;

    public async Task<Result<VehicleDto>> Handle(AddVehicleToCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);

        if (customer is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var vehicleResult = Vehicle.Create(
            Guid.NewGuid(),
            customer.Id,
            command.Make,
            command.Model,
            command.Year,
            command.LicensePlate,
            command.Vin);

        if (vehicleResult.IsError)
        {
            return vehicleResult.Errors;
        }

        // Customer-level guard (per-customer duplicates). Global duplicates are caught
        // by the database indexes below — the two layers cover different races.
        var addResult = customer.AddVehicle(vehicleResult.Value);

        if (addResult.IsError)
        {
            return addResult.Errors;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsVehicleVinUniqueViolation(exception))
        {
           _logger.LogWarning(exception, "Vehicle create rejected globally: VIN already exists.");

            return VehicleErrors.DuplicateVin;
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsVehicleLicensePlateUniqueViolation(exception))
        {
           _logger.LogWarning(exception, "Vehicle create rejected globally: license plate already exists.");

            return VehicleErrors.DuplicateLicensePlate;
        }

       _logger.LogInformation("Vehicle {VehicleId} added to customer {CustomerId}.", vehicleResult.Value.Id, customer.Id);

        return vehicleResult.Value.ToDto();
    }
}
