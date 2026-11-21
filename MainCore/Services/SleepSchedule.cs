namespace MainCore.Services;

/// <summary>A nightly sleep window in the account's own time zone, for example 23:30 to 07:00.</summary>
/// <param name="Start">Minutes after midnight when the night starts.</param>
/// <param name="End">Minutes after midnight when the night ends. Smaller than <paramref name="Start"/> when the window crosses midnight.</param>
/// <param name="Jitter">Random shift of each edge, in minutes either way, drawn again every time.</param>
public sealed record NightWindow(int Start, int End, int Jitter)
{
    private const int MinutesPerDay = 24 * 60;

    public bool Contains(TimeOnly time)
    {
        var minute = time.Hour * 60 + time.Minute;
        return Start <= End
            ? minute >= Start && minute < End
            : minute >= Start || minute < End;
    }

    /// <summary>The next time the night starts, strictly after <paramref name="after"/>, with jitter applied.</summary>
    public DateTime NextStart(DateTime after, Random random) => NextEdge(after, Start, random);

    /// <summary>The next time the night ends, strictly after <paramref name="after"/>, with jitter applied.</summary>
    public DateTime NextEnd(DateTime after, Random random) => NextEdge(after, End, random);

    private DateTime NextEdge(DateTime after, int minuteOfDay, Random random)
    {
        var edge = after.Date.AddMinutes(((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay);
        if (edge <= after) edge = edge.AddDays(1);

        var jitter = Jitter > 0 ? random.Next(-Jitter, Jitter + 1) : 0;
        var shifted = edge.AddMinutes(jitter);
        return shifted > after ? shifted : edge;
    }
}

/// <summary>
/// When the bot stops for sleep and when it wakes up. All times are in the account's time zone;
/// <see cref="SleepPlanner"/> converts to and from the machine's clock.
/// </summary>
public static class SleepSchedule
{
    /// <summary>
    /// When the current work session ends: after <paramref name="workTime"/>, or when the night starts, whichever comes first.
    /// Starting the bot at night by hand doesn't send it straight to sleep; the next night does.
    /// </summary>
    public static DateTime NextSleepStart(DateTime now, TimeSpan workTime, NightWindow? night, Random random)
    {
        var workEnd = now + workTime;
        if (night is null) return workEnd;

        var nightStart = night.NextStart(now, random);
        return nightStart < workEnd ? nightStart : workEnd;
    }

    /// <summary>
    /// When to wake up after a sleep that starts at <paramref name="sleepStart"/>.
    /// At night, sleep until the night ends. Otherwise sleep for <paramref name="sleepTime"/>,
    /// but if that would wake the bot in the middle of the night, keep sleeping until the night ends.
    /// </summary>
    public static DateTime SleepEnd(DateTime sleepStart, TimeSpan sleepTime, NightWindow? night, Random random)
    {
        if (night is null) return sleepStart + sleepTime;

        // A night start with jitter can fall a little before the window, so allow for that.
        var nearNight = night.Contains(TimeOnly.FromDateTime(sleepStart))
            || night.Contains(TimeOnly.FromDateTime(sleepStart.AddMinutes(night.Jitter)));
        if (nearNight)
        {
            return night.NextEnd(sleepStart, random);
        }

        var end = sleepStart + sleepTime;
        if (night.Contains(TimeOnly.FromDateTime(end)))
        {
            return night.NextEnd(end, random);
        }

        return end;
    }
}
