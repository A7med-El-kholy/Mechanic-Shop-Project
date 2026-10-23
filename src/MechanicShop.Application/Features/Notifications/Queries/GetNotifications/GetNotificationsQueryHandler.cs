using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Notifications.Dtos;
using MechanicShop.Application.Features.Notifications.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler(IAppDbContext context, IUser user)
    : IRequestHandler<GetNotificationsQuery, Result<PaginatedList<NotificationDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;

    public async Task<Result<PaginatedList<NotificationDto>>> Handle(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await NotificationScopeResolver.ResolveAsync(_context, _user, query.Scope, cancellationToken);

        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var notifications = query.Scope == NotificationAudience.Customer
            ? _context.Notifications.Where(n =>
                n.Audience == NotificationAudience.Customer &&
                n.CustomerId == scope.CustomerId)
            : _context.Notifications.Where(n => n.Audience == NotificationAudience.Staff);

        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => !n.IsRead);
        }

        var count = await notifications.CountAsync(cancellationToken);

        var items = await notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedOn)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<NotificationDto>(items.ToDtos(), page, pageSize, count);
    }
}
