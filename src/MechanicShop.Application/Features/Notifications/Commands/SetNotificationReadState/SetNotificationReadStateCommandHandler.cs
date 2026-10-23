using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Notifications.Commands.SetNotificationReadState;

public sealed class SetNotificationReadStateCommandHandler(IAppDbContext context, IUser user)
    : IRequestHandler<SetNotificationReadStateCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<Updated>> Handle(
        SetNotificationReadStateCommand command,
        CancellationToken cancellationToken)
    {
        var scope = await NotificationScopeResolver.ResolveAsync(
            _context,
            _user,
            command.Scope,
            cancellationToken);

        if (scope.Error is not null)
        {
            return scope.Error;
        }

        // The audience check lives in the WHERE clause: touching the other
        // inbox (or another customer's copy) is a 404, not a leak.
        var notification = command.Scope == NotificationAudience.Customer
            ? await _context.Notifications.FirstOrDefaultAsync(
                n => n.Id == command.NotificationId &&
                     n.Audience == NotificationAudience.Customer &&
                     n.CustomerId == scope.CustomerId,
                cancellationToken)
            : await _context.Notifications.FirstOrDefaultAsync(
                n => n.Id == command.NotificationId &&
                     n.Audience == NotificationAudience.Staff,
                cancellationToken);

        if (notification is null)
        {
            return NotificationErrors.NotFound(command.NotificationId);
        }

        notification.SetReadState(command.IsRead, DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Updated;
    }
}
