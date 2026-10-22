namespace MechanicShop.Domain.WorkOrders.Enums;

/// <summary>
/// Work order lifecycle. Stored as int in the database; never persisted as string —
/// values are stable identifiers, their numeric order must not be assigned meaning
/// beyond the transition table.
/// </summary>
public enum WorkOrderState
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}
