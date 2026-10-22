using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler(
    IAppDbContext context,
    IIdentityService identityService,
    ILogger<CreateCustomerCommandHandler> logger)
    : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly IIdentityService _identityService = identityService;
    private readonly ILogger<CreateCustomerCommandHandler> _logger = logger;

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        // Application-level duplicate check: the fast path. A concurrent insert can
        // still race past it; the same-email CHECK on the user table is what Identity
        // guarantees. This keeps the users table the source of truth for accounts.
        var exists = await _context.Customers
            .AnyAsync(c => c.Email!.ToLower() == email, cancellationToken);

        if (exists)
        {
            _logger.LogWarning("Customer creation aborted. Email {Email} already exists.", email);

            return CustomerErrors.CustomerExists;
        }

        // The customer id is generated first so every vehicle is born owning the FK;
        // Vehicle.CustomerId is write-once from Create, not mutated after the fact.
        var customerId = Guid.NewGuid();

        var vehicles = new List<Vehicle>(capacity: command.Vehicles.Count);

        foreach (var vehiclePayload in command.Vehicles)
        {
            var vehicleResult = Vehicle.Create(
                Guid.NewGuid(),
                customerId,
                vehiclePayload.Make,
                vehiclePayload.Model,
                vehiclePayload.Year,
                vehiclePayload.LicensePlate,
                vehiclePayload.Vin);

            if (vehicleResult.IsError)
            {
                return vehicleResult.Errors;
            }

            vehicles.Add(vehicleResult.Value);
        }

        var createResult = Customer.Create(
            customerId,
            command.Name.Trim(),
            command.PhoneNumber.Trim(),
            command.Email.Trim(),
            vehicles);

        if (createResult.IsError)
        {
            return createResult.Errors;
        }

        var customer = createResult.Value;

        _context.Customers.Add(customer);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsVehicleVinUniqueViolation(exception))
        {
            _logger.LogWarning(exception, "Customer creation aborted. A vehicle VIN already exists.");

            return VehicleErrors.DuplicateVin;
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsVehicleLicensePlateUniqueViolation(exception))
        {
            _logger.LogWarning(exception, "Customer creation aborted. A vehicle license plate already exists.");

            return VehicleErrors.DuplicateLicensePlate;
        }

        _logger.LogInformation("Customer {CustomerId} created.", customer.Id);

        // The workshop is the source of truth for customer profiles, but accounts are
        // self-registered. When a profile lands with an email that already belongs to
        // a Customer-role user and that user has no profile yet, link them immediately
        // so "my vehicles / my profile" views resolve from the very first login.
        var linkedUserId = await _identityService.FindUserIdByEmailAsync(email, cancellationToken);

        if (linkedUserId is not null)
        {
            var alreadyLinked = await _context.Customers
                .AnyAsync(c => c.UserId == linkedUserId, cancellationToken);

            if (!alreadyLinked)
            {
                var linkResult = customer.LinkUserAccount(linkedUserId);

                if (linkResult.IsSuccess)
                {
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Customer {CustomerId} linked to existing user {UserId}.",
                        customer.Id,
                        linkedUserId);
                }
            }
        }

        return customer.ToDto();
    }
}
