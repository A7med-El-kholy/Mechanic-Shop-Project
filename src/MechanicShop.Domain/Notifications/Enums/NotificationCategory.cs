namespace MechanicShop.Domain.Notifications.Enums;

/// <summary>
/// What the notification is about — the prototype's cat badges. The values are
/// stable identifiers; only the set that real events can produce is listed
/// (no AI-triage or fake-email categories here, because those features don't exist).
/// </summary>
public enum NotificationCategory
{
    Repairs = 0,
    Billing = 1,
    Inventory = 2,
    Orders = 3,
    Requests = 4,
    Assignments = 5
}
