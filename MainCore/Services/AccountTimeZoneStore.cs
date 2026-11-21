using System.Collections.Concurrent;

namespace MainCore.Services;

/// <summary>
/// The time zone each account plays in, as resolved when its browser last opened
/// (fixed in the settings, or looked up from the current IP). Defaults to this machine's zone.
/// </summary>
[RegisterSingleton<AccountTimeZoneStore>]
public sealed class AccountTimeZoneStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<AccountId, TimeZoneInfo> _zones = new();

    public TimeZoneInfo Get(AccountId accountId) =>
        _zones.TryGetValue(accountId, out var zone) ? zone : timeProvider.LocalTimeZone;

    public void Set(AccountId accountId, TimeZoneInfo zone) => _zones[accountId] = zone;
}
