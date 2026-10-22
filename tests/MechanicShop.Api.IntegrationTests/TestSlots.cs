namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Hands each booking in a test run a disjoint 30-minute block (date,
/// minute-of-day, bay), so parallel-created work orders never trip the
/// scheduling conflict rules accidentally — tests that specifically study
/// conflicts build them on top of an owned block.
///
/// 24 blocks/day × 3 bays, days roll over as the counter climbs, so a full run
/// never exhausts the book.
/// </summary>
internal static class TestSlots
{
    private const int BlocksPerDay = 24; // (20:00 − 08:00) / 30 minutes

    private const int BayCount = 3;

    private static int _counter = -1;

    /// <summary>One uniquely-owned 30-minute block.</summary>
    public static (DateOnly Date, int Minute, int Spot) Next()
    {
        var slot = Interlocked.Increment(ref _counter);

        var dayOffset = slot / (BlocksPerDay * BayCount);
        var within = slot % (BlocksPerDay * BayCount);

        var spot = within / BlocksPerDay;
        var minute = 480 + (within % BlocksPerDay) * 30;

        var date = new DateOnly(2027, 1, 15).AddDays(dayOffset);

        return (date, minute, spot);
    }

    /// <summary>Two consecutive 30-minute blocks in the same bay (adjacency tests).</summary>
    public static ((DateOnly Date, int Minute, int Spot) First, (DateOnly Date, int Minute, int Spot) Second) NextPair()
    {
        while (true)
        {
            var first = Next();
            var second = Next();

            var contiguous = first.Date == second.Date &&
                first.Spot == second.Spot &&
                second.Minute == first.Minute + 30;

            if (contiguous)
            {
                return (first, second);
            }
        }
    }
}
