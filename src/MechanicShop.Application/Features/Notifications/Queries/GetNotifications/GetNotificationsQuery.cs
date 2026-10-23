using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Notifications.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

namespace MechanicShop.Application.Features.Notifications.Queries.GetNotifications;

/// <summary>Newest first, optionally only unread. The same query serves the
/// staff inbox and a customer's portal inbox — the Scope decides whose.</summary>
public sealed record GetNotificationsQuery(
    NotificationAudience Scope,
    bool UnreadOnly,
    int Page,
    int PageSize) : IRequest<Result<PaginatedList<NotificationDto>>>;
