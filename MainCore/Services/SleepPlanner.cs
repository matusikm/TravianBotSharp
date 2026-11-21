namespace MainCore.Services;

/// <summary>
/// Applies <see cref="SleepSchedule"/> to an account: reads its settings, works in the account's time zone,
/// and returns times on this machine's clock, which is what the scheduler uses.
/// </summary>
[RegisterSingleton<SleepPlanner>]
public sealed class SleepPlanner(AccountSettingCache settingCache, AccountTimeZoneStore timeZoneStore, TimeProvider timeProvider)
{
    public WorkEndActionEnums GetWorkEndAction(AccountId accountId) =>
        (WorkEndActionEnums)settingCache.Get(accountId, AccountSettingEnums.WorkEndAction);

    /// <summary>When the current work session ends, on this machine's clock.</summary>
    public DateTime NextSleepStart(AccountId accountId)
    {
        var zone = timeZoneStore.Get(accountId);
        var now = AccountNow(zone);
        var workTime = TimeSpan.FromSeconds(settingCache.Random(accountId, AccountSettingEnums.WorkTimeMin, AccountSettingEnums.WorkTimeMax, 60));
        var start = SleepSchedule.NextSleepStart(now, workTime, GetNight(accountId), Random.Shared);
        return ToMachineTime(start, zone);
    }

    /// <summary>When a sleep that starts now should end, on this machine's clock.</summary>
    public DateTime SleepEnd(AccountId accountId)
    {
        var zone = timeZoneStore.Get(accountId);
        var now = AccountNow(zone);
        var sleepTime = TimeSpan.FromSeconds(settingCache.Random(accountId, AccountSettingEnums.SleepTimeMin, AccountSettingEnums.SleepTimeMax, 60));
        var end = SleepSchedule.SleepEnd(now, sleepTime, GetNight(accountId), Random.Shared);
        return ToMachineTime(end, zone);
    }

    private NightWindow? GetNight(AccountId accountId)
    {
        if (settingCache.Get(accountId, AccountSettingEnums.EnableNightSleep) == 0) return null;
        return new NightWindow(
            settingCache.Get(accountId, AccountSettingEnums.NightSleepStart),
            settingCache.Get(accountId, AccountSettingEnums.NightSleepEnd),
            settingCache.Get(accountId, AccountSettingEnums.NightSleepJitter));
    }

    private DateTime AccountNow(TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime;

    private DateTime ToMachineTime(DateTime accountTime, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(accountTime, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified)) unspecified = unspecified.AddHours(1);
        var offset = new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
        return TimeZoneInfo.ConvertTime(offset, timeProvider.LocalTimeZone).DateTime;
    }
}
