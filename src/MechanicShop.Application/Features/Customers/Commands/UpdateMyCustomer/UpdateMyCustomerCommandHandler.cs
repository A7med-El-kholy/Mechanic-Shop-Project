using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateMyCustomer;

public sealed class UpdateMyCustomerCommandHandler(
    IAppDbContext context,
    IUser user,
    ILogger<UpdateMyCustomerCommandHandler> logger)
    : IRequestHandler<UpdateMyCustomerCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;
    private readonly ILogger<UpdateMyCustomerCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(
        UpdateMyCustomerCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            return Error.NotFound(
                "Customer_ProfileNotLinked",
                "No customer profile is linked to this account yet.");
        }

        // Email stays exactly as the workshop/identity store has it.
        var updateResult = customer.Update(
            command.Name.Trim(),
            customer.Email,
            command.PhoneNumber.Trim());

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} updated their own profile.", customer.Id);

        return Result.Updated;
    }
}
