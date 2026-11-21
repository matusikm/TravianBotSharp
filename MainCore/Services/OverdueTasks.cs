namespace MainCore.Services;

/// <summary>
/// After an outage every task that fell due meanwhile would run back to back, a burst no person produces.
/// This keeps them in order but moves them apart.
/// </summary>
public static class OverdueTasks
{
    /// <summary>Shortest and longest gap between two overdue tasks, in seconds (skewed towards the short end).</summary>
    public const int MinGapSeconds = 20;
    public const int MaxGapSeconds = 120;

    /// <summary>
    /// The earliest overdue task stays due now (usually the one the outage interrupted); each later overdue task
    /// is moved to a random gap after the one before. Tasks that are not due yet are left alone.
    /// Returns how many tasks were moved.
    /// </summary>
    public static int Spread(IEnumerable<BaseTask> tasks, DateTime now, Random random)
    {
        var overdue = tasks
            .Where(x => x.ExecuteAt <= now)
            .OrderBy(x => x.ExecuteAt)
            .ToList();

        var next = now;
        for (var i = 1; i < overdue.Count; i++)
        {
            next = next.AddSeconds(HumanTiming.SkewedDelay(random, MinGapSeconds, MaxGapSeconds));
            overdue[i].ExecuteAt = next;
        }

        return Math.Max(overdue.Count - 1, 0);
    }
}
