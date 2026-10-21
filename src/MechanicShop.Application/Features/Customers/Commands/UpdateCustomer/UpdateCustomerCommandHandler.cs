using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(
    IAppDbContext context,
    ILogger<UpdateCustomerCommandHandler> logger)
    : IRequestHandler<UpdateCustomerCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateCustomerCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("Customer update aborted. Customer {CustomerId} was not found.", command.CustomerId);

            return CustomerErrors.NotFound(command.CustomerId);
        }

        var email = command.Email.Trim().ToLowerInvariant();

        // Email must stay unique across customers, but the customer must not collide
        // with itself — hence the Id filter.
        var takenByAnother = await _context.Customers
            .AnyAsync(c => c.Id != command.CustomerId && c.Email!.ToLower() == email, cancellationToken);

        if (takenByAnother)
        {
            _logger.LogWarning("Customer update aborted. Email {Email} belongs to another customer.", email);

            return CustomerErrors.CustomerExists;
        }

        var updateResult = customer.Update(
            command.Name.Trim(),
            command.Email.Trim(),
            command.PhoneNumber.Trim());

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} updated.", command.CustomerId);

        return Result.Updated;
    }
}
