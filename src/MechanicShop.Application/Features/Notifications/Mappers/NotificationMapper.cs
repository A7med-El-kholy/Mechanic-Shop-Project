using MechanicShop.Application.Features.Notifications.Dtos;
using MechanicShop.Domain.Notifications;

namespace MechanicShop.Application.Features.Notifications.Mappers;

public static class NotificationMapper
{
    public static NotificationDto ToDto(this Notification notification)
    {
        return new NotificationDto(
            notification.Id,
            (int)notification.Audience,
            notification.Title,
            notification.Message,
            (int)notification.Category,
            notification.WorkOrderId,
            notification.IsRead,
            notification.CreatedOn,
            notification.ReadOn);
    }

    public static List<NotificationDto> ToDtos(this IEnumerable<Notification> notifications)
    {
        return [.. notifications.Select(ToDto)];
    }
}
