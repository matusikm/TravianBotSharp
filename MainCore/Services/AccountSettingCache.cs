using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace MainCore.Services;

/// <summary>
/// Account settings read on hot paths (every click, every task) without a database round trip.
/// Settings are loaded per account on first use and dropped when they are saved through <see cref="Save"/>.
/// </summary>
[RegisterSingleton<AccountSettingCache>]
public sealed class AccountSettingCache(IDbContextFactory<AppDbContext> contextFactory)
{
    private readonly ConcurrentDictionary<AccountId, FrozenDictionary<AccountSettingEnums, int>> _settings = new();

    public int Get(AccountId accountId, AccountSettingEnums setting) =>
        Load(accountId).GetValueOrDefault(setting);

    /// <summary>
    /// A random value between two settings (in either order), times <paramref name="multiplier"/>.
    /// Returns 1000 when either setting is missing, like the database lookup did.
    /// </summary>
    public int Random(AccountId accountId, AccountSettingEnums settingMin, AccountSettingEnums settingMax, int multiplier = 1)
    {
        var settings = Load(accountId);
        if (!settings.TryGetValue(settingMin, out var first) || !settings.TryGetValue(settingMax, out var second))
        {
            return 1000;
        }

        var min = Math.Min(first, second);
        var max = Math.Max(first, second);
        return System.Random.Shared.Next(min * multiplier, max * multiplier);
    }

    /// <summary>
    /// Like <see cref="Random"/>, but shaped like human timing: usually near the low end, sometimes higher,
    /// and now and then a much longer pause (see <see cref="HumanTiming.SkewedDelay"/>).
    /// </summary>
    public int Skewed(AccountId accountId, AccountSettingEnums settingMin, AccountSettingEnums settingMax)
    {
        var settings = Load(accountId);
        if (!settings.TryGetValue(settingMin, out var first) || !settings.TryGetValue(settingMax, out var second))
        {
            return 1000;
        }

        return HumanTiming.SkewedDelay(System.Random.Shared, first, second);
    }

    /// <summary>Writes settings to the database and refreshes the cache for that account.</summary>
    public void Save(AccountId accountId, Dictionary<AccountSettingEnums, int> settings)
    {
        settings.Remove(AccountSettingEnums.Tribe);
        if (settings.Count == 0) return;

        using (var context = contextFactory.CreateDbContext())
        {
            foreach (var setting in settings)
            {
                context.AccountsSetting
                    .Where(x => x.AccountId == accountId.Value)
                    .Where(x => x.Setting == setting.Key)
                    .ExecuteUpdate(x => x.SetProperty(x => x.Value, setting.Value));
            }
        }

        Invalidate(accountId);
    }

    public void Invalidate(AccountId accountId) => _settings.TryRemove(accountId, out _);

    private FrozenDictionary<AccountSettingEnums, int> Load(AccountId accountId) =>
        _settings.GetOrAdd(accountId, id =>
        {
            using var context = contextFactory.CreateDbContext();
            return context.AccountsSetting
                .Where(x => x.AccountId == id.Value)
                .AsEnumerable()
                .GroupBy(x => x.Setting)
                .ToFrozenDictionary(x => x.Key, x => x.First().Value);
        });
}
