using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

namespace MechanicShop.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

/// <summary>The bell badge: one cheap COUNT per inbox, no rows shipped.</summary>
public sealed record GetUnreadNotificationCountQuery(NotificationAudience Scope)
    : IRequest<Result<int>>;
