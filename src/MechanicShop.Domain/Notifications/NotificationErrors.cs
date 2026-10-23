using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Notifications;

public static class NotificationErrors
{
    public static Error TitleRequired =>
        Error.Validation("Notification_TitleRequired", "A notification needs a title.");

    public static Error TitleTooLong =>
        Error.Validation(
            "Notification_TitleTooLong",
            $"A notification title must be at most {Notification.TitleMaxLength} characters.");

    public static Error MessageRequired =>
        Error.Validation("Notification_MessageRequired", "A notification needs a message.");

    public static Error MessageTooLong =>
        Error.Validation(
            "Notification_MessageTooLong",
            $"A notification message must be at most {Notification.MessageMaxLength} characters.");

    public static Error CustomerRequired =>
        Error.Validation(
            "Notification_CustomerRequired",
            "A customer notification must name the customer it belongs to.");

    public static Error NotFound(Guid notificationId) =>
        Error.NotFound(
            "Notification_NotFound",
            $"Notification '{notificationId}' was not found.");
}
