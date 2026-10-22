using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Appointments.Commands.CreateAppointmentRequest;

public sealed class CreateAppointmentRequestCommandHandler(
    IAppDbContext context,
    IUser user,
    ILogger<CreateAppointmentRequestCommandHandler> logger)
    : IRequestHandler<CreateAppointmentRequestCommand, Result<Created>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;
    private readonly ILogger<CreateAppointmentRequestCommandHandler> _logger = logger;

    public async Task<Result<Created>> Handle(CreateAppointmentRequestCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            return AppointmentRequestErrors.NoProfileLinked;
        }

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            _logger.LogWarning("Request blocked: vehicle {VehicleId} does not exist.", command.VehicleId);

            return VehicleErrors.NotFound(command.VehicleId);
        }

        // Ownership is enforced here, not by an URL id somebody could try to shuffle:
        // another customer's plate, or an archived car, cannot be booked.
        if (vehicle.CustomerId != customer.Id || vehicle.IsArchived)
        {
            _logger.LogWarning(
                "Appointment request blocked: customer {CustomerId} does not own live vehicle {VehicleId}.",
                customer.Id,
                command.VehicleId);

            return AppointmentRequestErrors.VehicleNotOwned;
        }

        if (command.ServiceTaskId is not null)
        {
            var taskExists = await _context.ServiceTasks
                .AnyAsync(t => t.Id == command.ServiceTaskId, cancellationToken);

            if (!taskExists)
            {
                return ServiceTaskErrors.NotFound(command.ServiceTaskId.Value);
            }
        }

        var created = AppointmentRequest.Create(
            Guid.NewGuid(),
            customer.Id,
            vehicle.Id,
            command.ServiceTaskId,
            command.PreferredDate);

        if (created.IsError)
        {
            return created.TopError;
        }

        _context.AppointmentRequests.Add(created.Value);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appointment request {RequestId} created by customer {CustomerId} for vehicle {VehicleId} on {PreferredDate}.",
            created.Value.Id,
            customer.Id,
            vehicle.Id,
            command.PreferredDate);

        return Result.Created;
    }
}
