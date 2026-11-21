namespace MainCore.Services;

/// <summary>
/// Random numbers shaped like a person's timing rather than a flat dice roll.
/// Everything takes a <see cref="Random"/> so tests can seed it.
/// </summary>
public static class HumanTiming
{
    /// <summary>Chance that a delay turns into a much longer pause, like a person glancing away.</summary>
    public const double LongPauseChance = 0.03;

    /// <summary>
    /// A delay between <paramref name="min"/> and <paramref name="max"/> that is usually near the
    /// low end and sometimes near the top (log-normal), plus a rare long pause of 2-5 times the value.
    /// </summary>
    public static int SkewedDelay(Random random, int min, int max)
    {
        if (max < min) (min, max) = (max, min);
        if (max == min) return min;

        // Median at about a third of the range, long tail to the right. Values past the top are drawn again.
        const double median = 0.33;
        const double sigma = 0.55;
        double fraction;
        var attempts = 0;
        do
        {
            fraction = median * Math.Exp(sigma * NextGaussian(random));
            attempts++;
        }
        while (fraction > 1 && attempts < 10);
        fraction = Math.Clamp(fraction, 0, 1);

        var value = min + (max - min) * fraction;
        if (random.NextDouble() < LongPauseChance)
        {
            value *= 2 + random.NextDouble() * 3;
        }

        return (int)Math.Round(value);
    }

    /// <summary>Delay between two key presses, in milliseconds.</summary>
    public static int KeyDelay(Random random) => SkewedDelay(random, 45, 260);

    /// <summary>
    /// A point inside a box of the given size, clustered around the middle and kept away from the edges,
    /// so clicks don't all land on the exact centre.
    /// </summary>
    public static (float X, float Y) PointInBox(Random random, float width, float height)
    {
        return (Coordinate(random, width), Coordinate(random, height));

        static float Coordinate(Random random, float size)
        {
            if (size <= 2) return size / 2;
            var offset = NextGaussian(random) * 0.15;
            var fraction = Math.Clamp(0.5 + offset, 0.2, 0.8);
            return (float)(size * fraction);
        }
    }

    /// <summary>
    /// Default settings for a new account, with timings shifted at random so that accounts don't all
    /// share the same click, task, farm and sleep ranges.
    /// </summary>
    public static Dictionary<AccountSettingEnums, int> RandomizedAccountDefaults(Random random)
    {
        var settings = AppDbContext.AccountDefaultSettings.ToDictionary(x => x.Key, x => x.Value);

        SetRange(AccountSettingEnums.ClickDelayMin, AccountSettingEnums.ClickDelayMax, random.Next(350, 651), random.Next(250, 551));
        SetRange(AccountSettingEnums.TaskDelayMin, AccountSettingEnums.TaskDelayMax, random.Next(700, 1301), random.Next(300, 801));
        SetRange(AccountSettingEnums.FarmIntervalMin, AccountSettingEnums.FarmIntervalMax, random.Next(480, 601), random.Next(60, 181));
        SetRange(AccountSettingEnums.WorkTimeMin, AccountSettingEnums.WorkTimeMax, random.Next(9, 11) * 60, random.Next(1, 3) * 60);
        SetRange(AccountSettingEnums.SleepTimeMin, AccountSettingEnums.SleepTimeMax, random.Next(420, 541), random.Next(60, 121));

        // Bedtime between 22:30 and 00:30, wake-up between 06:30 and 08:00.
        settings[AccountSettingEnums.NightSleepStart] = (random.Next(22 * 60 + 30, 24 * 60 + 31)) % (24 * 60);
        settings[AccountSettingEnums.NightSleepEnd] = random.Next(6 * 60 + 30, 8 * 60 + 1);
        settings[AccountSettingEnums.NightSleepJitter] = random.Next(30, 61);

        return settings;

        void SetRange(AccountSettingEnums minSetting, AccountSettingEnums maxSetting, int min, int width)
        {
            settings[minSetting] = min;
            settings[maxSetting] = min + width;
        }
    }

    private static double NextGaussian(Random random)
    {
        // Box-Muller.
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
