namespace MechanicShop.Domain.Notifications.Enums;

/// <summary>
/// Who sees a notification. The prototype folds this into a letter string
/// ('MT', 'C', ...); the real system keeps it explicit and typed. Staff share
/// one inbox today (one staff identity exists); customer rows always name
/// exactly one customer — the portal data wall then applies like everywhere else.
/// </summary>
public enum NotificationAudience
{
    Staff = 0,
    Customer = 1
}
