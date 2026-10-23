using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Notifications.Enums;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Domain.Notifications;

/// <summary>
/// An event copy for one audience: "the job moved", "the invoice was paid",
/// "the shelf is low". Notifications are derived from business facts that
/// already live in their own tables — they are never the source of truth and
/// never carry money or lifecycle state of their own. That is why read state
/// is a simple flag (one viewer per copy) and why the customer FK cascades:
/// deleting a profile takes its inbox with it, while the facts stay.
/// </summary>
public sealed class Notification : Entity
{
    public const int TitleMaxLength = 120;
    public const int MessageMaxLength = 600;

    public NotificationAudience Audience { get; private set; }

    /// <summary>Set exactly when Audience is Customer; staff copies carry no owner.</summary>
    public Guid? CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public NotificationCategory Category { get; private set; }

    /// <summary>Optional deep link: "Open" jumps to the work order this is about.</summary>
    public Guid? WorkOrderId { get; private set; }

    public WorkOrder? WorkOrder { get; private set; }

    public bool IsRead { get; private set; }

    /// <summary>Stored in UTC; the client renders relative time from it.</summary>
    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset? ReadOn { get; private set; }

#pragma warning disable CS8618

    private Notification()
    { }

#pragma warning restore CS8618

    private Notification(
        Guid id,
        NotificationAudience audience,
        Guid? customerId,
        string title,
        string message,
        NotificationCategory category,
        Guid? workOrderId,
        DateTimeOffset createdOn)
        : base(id)
    {
        Audience = audience;
        CustomerId = customerId;
        Title = title;
        Message = message;
        Category = category;
        WorkOrderId = workOrderId;
        CreatedOn = createdOn;
        IsRead = false;
    }

    /// <summary>The shared staff inbox. Staff actions notify staff; no owner column.</summary>
    public static Result<Notification> ForStaff(
        Guid id,
        string title,
        string message,
        NotificationCategory category,
        Guid? workOrderId,
        DateTimeOffset createdOn)
    {
        return Create(id, NotificationAudience.Staff, null, title, message, category, workOrderId, createdOn);
    }

    /// <summary>One customer's copy — visible only through the portal data wall.</summary>
    public static Result<Notification> ForCustomer(
        Guid id,
        Guid customerId,
        string title,
        string message,
        NotificationCategory category,
        Guid? workOrderId,
        DateTimeOffset createdOn)
    {
        if (customerId == Guid.Empty)
        {
            return NotificationErrors.CustomerRequired;
        }

        return Create(id, NotificationAudience.Customer, customerId, title, message, category, workOrderId, createdOn);
    }

    private static Result<Notification> Create(
        Guid id,
        NotificationAudience audience,
        Guid? customerId,
        string title,
        string message,
        NotificationCategory category,
        Guid? workOrderId,
        DateTimeOffset createdOn)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return NotificationErrors.TitleRequired;
        }

        var trimmedTitle = title.Trim();

        if (trimmedTitle.Length > TitleMaxLength)
        {
            return NotificationErrors.TitleTooLong;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return NotificationErrors.MessageRequired;
        }

        var trimmedMessage = message.Trim();

        if (trimmedMessage.Length > MessageMaxLength)
        {
            return NotificationErrors.MessageTooLong;
        }

        return new Notification(id, audience, customerId, trimmedTitle, trimmedMessage, category, workOrderId, createdOn);
    }

    /// <summary>
    /// Idempotent read-state setter (the prototype's toggle, made explicit):
    /// the client states what it wants, so a double click cannot flip twice.
    /// Setting the same state again is a no-op, not an error.
    /// </summary>
    public Result<Updated> SetReadState(bool isRead, DateTimeOffset now)
    {
        if (IsRead == isRead)
        {
            return Result.Updated;
        }

        IsRead = isRead;
        ReadOn = isRead ? now : null;

        return Result.Updated;
    }
}
