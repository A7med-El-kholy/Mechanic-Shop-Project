namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Hands each booking in a test run a day of its own (Bay A, opening slot), so
/// two independently-created work orders can never share a bay-time window.
///
/// A 30-minute grid is NOT enough: jobs may run for hours (a two-task job is
/// 90 minutes), so the next block would overlap a longer earlier job and trip
/// the scheduling rules by accident — a latent flake that shifted with test
/// order. Tests that deliberately overlap windows build their bodies from
/// explicit values instead; <see cref="NextPair"/> reserves one day for its two
/// back-to-back blocks.
/// </summary>
internal static class TestSlots
{
    private const int DayStartMinute = 480; // 08:00, the workshop's opening slot

    private const int BayA = 0;

    private static readonly DateOnly FirstDay = new(2027, 1, 15);

    private static int _day = -1;

    private static DateOnly NextDay() => FirstDay.AddDays(Interlocked.Increment(ref _day));

    /// <summary>One uniquely-owned day: its opening slot in Bay A.</summary>
    public static (DateOnly Date, int Minute, int Spot) Next()
    {
        return (NextDay(), DayStartMinute, BayA);
    }

    /// <summary>Two consecutive 30-minute blocks in the same bay on one owned day.</summary>
    public static ((DateOnly Date, int Minute, int Spot) First, (DateOnly Date, int Minute, int Spot) Second) NextPair()
    {
        var date = NextDay();

        return ((date, DayStartMinute, BayA), (date, DayStartMinute + 30, BayA));
    }
}
