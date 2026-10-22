using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Domain.WorkOrders;

/// <summary>
/// The workshop's operating day, as the prototype encodes it: lifts open at 08:00
/// and stop accepting new starts after 20:00, on a 15-minute grid (48 start
/// slots). All values are minute-of-day numbers; dates ride along on the work
/// order itself, which keeps scheduling free of timezone math.
///
/// Jobs may finish past 20:00 — only the start slot must fall inside the window.
/// Since every job is at least one grid slot long and starts no later than 19:45,
/// no job can cross midnight, so day-scoped overlap checks stay simple.
/// </summary>
public static class WorkDay
{
    public const int OpensAtMinute = 480;   // 08:00

    public const int ClosesAtMinute = 1200; // 20:00

    public const int GridStepMinutes = 15;

    /// <summary>Latest legal start slot: 19:45 (the prototype's 48th slot).</summary>
    public const int LastSlotStartsAtMinute = ClosesAtMinute - GridStepMinutes;

    /// <summary>The prototype floors every job at one grid slot of duration.</summary>
    public const int MinimumJobDurationMinutes = GridStepMinutes;

    public static bool IsGridSlot(int minute) =>
        minute % GridStepMinutes == 0 &&
        minute >= OpensAtMinute &&
        minute <= LastSlotStartsAtMinute;

    public static string FormatMinute(int minute) =>
        $"{minute / 60:D2}:{minute % 60:D2}";
}
