using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Inventory;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public sealed class RemoveCustomerCommandHandler(
    IAppDbContext context,
    ILogger<RemoveCustomerCommandHandler> logger)
    : IRequestHandler<RemoveCustomerCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveCustomerCommandHandler> _logger = logger;

    public async Task<Result<Deleted>> Handle(RemoveCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("Customer removal aborted. Customer {CustomerId} was not found.", command.CustomerId);

            return CustomerErrors.NotFound(command.CustomerId);
        }

        // Work orders reference vehicles, not the customer; the customer->vehicles
        // cascade would otherwise erase service history and active jobs silently.
        // Deleting a customer is only clean once no vehicle of theirs is cited by
        // any work order, past or live.
        var hasWorkOrders = await _context.WorkOrders
            .AnyAsync(w => _context.Vehicles
                .Any(v => v.Id == w.VehicleId && v.CustomerId == command.CustomerId),
                cancellationToken);

        if (hasWorkOrders)
        {
            _logger.LogWarning("Customer {CustomerId} removal blocked: their vehicles appear on work orders.", command.CustomerId);

            return CustomerErrors.HasWorkOrders(command.CustomerId);
        }

        // Pending appointment requests reference the vehicles path too; with the
        // requests FK restricted, a customer whose cars are requested cannot
        // cascade cleanly either.
        var hasPendingRequests = await _context.AppointmentRequests
            .AnyAsync(r => _context.Vehicles
                .Any(v => v.Id == r.VehicleId && v.CustomerId == command.CustomerId),
                cancellationToken);

        if (hasPendingRequests)
        {
            _logger.LogWarning(
                "Customer {CustomerId} removal blocked: their cars have pending appointment requests.",
                command.CustomerId);

            return CustomerErrors.HasPendingRequests(command.CustomerId);
        }

        // Shop orders FK straight to the customer with Restrict: buying history
        // anchors the profile the same way service history does.
        var hasShopOrders = await _context.ShopOrders
            .AnyAsync(o => o.CustomerId == command.CustomerId, cancellationToken);

        if (hasShopOrders)
        {
            _logger.LogWarning(
                "Customer {CustomerId} removal blocked: they hold parts shop orders.",
                command.CustomerId);

            return CustomerErrors.HasShopOrders(command.CustomerId);
        }

        // The cascade removes the customer's remaining vehicles with the row itself.
        _context.Customers.Remove(customer);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} removed.", command.CustomerId);

        return Result.Deleted;
    }
}
