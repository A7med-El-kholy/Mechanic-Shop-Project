using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Notifications.Enums;

using MediatR;

namespace MechanicShop.Application.Features.Notifications.Commands.SetNotificationReadState;

/// <summary>Explicit set, not a toggle: the client says what it wants the
/// state to be, so retries can never flip a row twice.</summary>
public sealed record SetNotificationReadStateCommand(
    Guid NotificationId,
    bool IsRead,
    NotificationAudience Scope) : IRequest<Result<Updated>>;
