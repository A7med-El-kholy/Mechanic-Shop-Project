using System.Globalization;

namespace MechanicShop.Application.Features.Notifications;

/// <summary>
/// Formatting shared by the event handlers that compose notification text.
/// Money is rendered with the invariant culture and an explicit "$" so the
/// message reads the same regardless of the server's locale — these strings
/// are stored, not re-formatted at read time.
/// </summary>
public static class NotificationFormat
{
    public static string Money(decimal amount) =>
        "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
