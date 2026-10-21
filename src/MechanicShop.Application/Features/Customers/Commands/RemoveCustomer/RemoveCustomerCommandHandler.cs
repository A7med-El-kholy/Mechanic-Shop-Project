using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;

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

        // Work-order existence guard arrives with the work-order feature; today the
        // cascade removes the customer's vehicles with the row itself.
        _context.Customers.Remove(customer);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} removed.", command.CustomerId);

        return Result.Deleted;
    }
}
