using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

namespace MechanicShop.Application.Features.Notifications.Commands.MarkNotificationsRead;

/// <summary>The prototype's "Mark all as read" — scoped so a customer can
/// never mark the staff inbox (or another customer's) in bulk.</summary>
public sealed record MarkNotificationsReadCommand(NotificationAudience Scope)
    : IRequest<Result<Updated>>;
