namespace MechanicShop.Domain.WorkOrders.Enums;

/// <summary>
/// Lift position (bay). The prototype exposes bays A, B and C on the scheduling
/// form and stores the chosen bay on each work order.
/// </summary>
public enum Spot
{
    A = 0,
    B = 1,
    C = 2
}
