using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Notifications;

/// <summary>The resolved scope for a notification operation: staff operations
/// carry no owner; customer operations must point at a linked profile.</summary>
internal sealed record NotificationScopeContext(Guid? CustomerId, Error? Error);

/// <summary>
/// One place that answers "whose inbox is this?" — the same social contract as
/// the rest of the portal: an account without a linked customer profile gets
/// NoProfileLinked, and reading another customer's copy is indistinguishable
/// from a copy that does not exist.
/// </summary>
internal static class NotificationScopeResolver
{
    public static async Task<NotificationScopeContext> ResolveAsync(
        IAppDbContext context,
        IUser user,
        NotificationAudience scope,
        CancellationToken cancellationToken)
    {
        if (scope == NotificationAudience.Staff)
        {
            return new NotificationScopeContext(null, null);
        }

        if (string.IsNullOrWhiteSpace(user.UserId))
        {
            return new NotificationScopeContext(
                null,
                Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier."));
        }

        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.UserId == user.UserId, cancellationToken);

        return customer is null
            ? new NotificationScopeContext(null, AppointmentRequestErrors.NoProfileLinked)
            : new NotificationScopeContext(customer.Id, null);
    }
}
