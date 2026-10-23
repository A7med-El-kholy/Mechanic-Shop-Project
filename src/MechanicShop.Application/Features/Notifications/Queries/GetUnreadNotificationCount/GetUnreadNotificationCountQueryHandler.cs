using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public sealed class GetUnreadNotificationCountQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<int>> Handle(
        GetUnreadNotificationCountQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await NotificationScopeResolver.ResolveAsync(_context, _user, query.Scope, cancellationToken);

        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var unread = query.Scope == NotificationAudience.Customer
            ? _context.Notifications.Where(n =>
                n.Audience == NotificationAudience.Customer &&
                n.CustomerId == scope.CustomerId &&
                !n.IsRead)
            : _context.Notifications.Where(n =>
                n.Audience == NotificationAudience.Staff &&
                !n.IsRead);

        return await unread.CountAsync(cancellationToken);
    }
}
