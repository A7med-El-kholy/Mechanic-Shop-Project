namespace MechanicShop.Application.Features.Notifications.Dtos;

/// <summary>One notification row: what happened, which category, and whether
/// it has been read. WorkOrderId, when present, powers the "Open" button.</summary>
public sealed record NotificationDto(
    Guid NotificationId,
    int Audience,
    string Title,
    string Message,
    int Category,
    Guid? WorkOrderId,
    bool IsRead,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ReadOn);
