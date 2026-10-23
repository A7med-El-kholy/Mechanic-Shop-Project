namespace MechanicShop.Api.Features.Notifications.Contracts;

/// <summary>
/// Explicit target state for one row — never a toggle. The client states what it
/// wants, so a retried or double-clicked request cannot flip the row twice.
/// </summary>
public sealed record SetNotificationReadStateRequest(bool IsRead);

/// <summary>The bell badge payload: a count, not rows.</summary>
public sealed record UnreadNotificationsResponse(int Count);
