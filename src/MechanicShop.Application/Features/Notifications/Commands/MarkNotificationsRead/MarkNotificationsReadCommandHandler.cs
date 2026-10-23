using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Notifications.Commands.MarkNotificationsRead;

public sealed class MarkNotificationsReadCommandHandler(
    IAppDbContext context,
    IUser user,
    ILogger<MarkNotificationsReadCommandHandler> logger)
    : IRequestHandler<MarkNotificationsReadCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;
    private readonly ILogger<MarkNotificationsReadCommandHandler> _logger = logger;

    public async Task<Result<Updated>> Handle(
        MarkNotificationsReadCommand command,
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

        var now = DateTimeOffset.UtcNow;

        // One UPDATE statement, not one per row: bulk read-state is a single
        // fact ("everything I can see is read"), so it executes as one fact.
        var unread = command.Scope == NotificationAudience.Customer
            ? _context.Notifications.Where(n =>
                n.Audience == NotificationAudience.Customer &&
                n.CustomerId == scope.CustomerId &&
                !n.IsRead)
            : _context.Notifications.Where(n =>
                n.Audience == NotificationAudience.Staff &&
                !n.IsRead);

        var updated = await unread.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadOn, (DateTimeOffset?)now),
            cancellationToken);

        _logger.LogInformation(
            "Marked {Count} notification(s) read for {Scope}.",
            updated,
            command.Scope);

        return Result.Updated;
    }
}
