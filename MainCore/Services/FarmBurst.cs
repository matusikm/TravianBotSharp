using System.Collections.Concurrent;

namespace MainCore.Services;

/// <summary>
/// Farming in bursts: a few sends at the normal farm interval, then an idle gap, with the odd cycle skipped.
/// One state per account; pure logic so it can be tested with a seeded <see cref="Random"/>.
/// </summary>
public sealed class FarmBurst
{
    public const double SkipChance = 0.1;
    public const int MinBurstSize = 2;
    public const int MaxBurstSize = 5;

    private int _remainingInBurst = -1;

    /// <summary>True when this cycle should be skipped, as a person sometimes doesn't bother.</summary>
    public static bool ShouldSkip(Random random) => random.NextDouble() < SkipChance;

    /// <summary>
    /// Time until the next farm cycle. Inside a burst it is the farm interval (seconds);
    /// after the last send of a burst it is the idle gap (minutes), and a new burst starts.
    /// </summary>
    public TimeSpan NextDelay(Random random, (int Min, int Max) intervalSeconds, (int Min, int Max) idleMinutes)
    {
        if (_remainingInBurst < 0)
        {
            // First call for this account: start somewhere inside a burst.
            _remainingInBurst = random.Next(0, MaxBurstSize);
        }

        if (_remainingInBurst > 0)
        {
            _remainingInBurst--;
            return TimeSpan.FromSeconds(HumanTiming.SkewedDelay(random, intervalSeconds.Min, intervalSeconds.Max));
        }

        _remainingInBurst = random.Next(MinBurstSize, MaxBurstSize + 1) - 1;
        var (idleMin, idleMax) = idleMinutes.Min <= idleMinutes.Max ? idleMinutes : (idleMinutes.Max, idleMinutes.Min);
        var idleSeconds = random.Next(idleMin * 60, idleMax * 60 + 1);
        return TimeSpan.FromSeconds(idleSeconds);
    }
}

[RegisterSingleton<FarmBurstPlanner>]
public sealed class FarmBurstPlanner(AccountSettingCache settingCache, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<AccountId, FarmBurst> _bursts = new();

    /// <summary>When the next farm cycle should run, on this machine's clock.</summary>
    public DateTime NextRun(AccountId accountId)
    {
        var burst = _bursts.GetOrAdd(accountId, _ => new FarmBurst());
        TimeSpan delay;
        lock (burst)
        {
            delay = burst.NextDelay(
                Random.Shared,
                Range(AccountSettingEnums.FarmIntervalMin, AccountSettingEnums.FarmIntervalMax),
                Range(AccountSettingEnums.FarmIdleMin, AccountSettingEnums.FarmIdleMax));
        }
        return timeProvider.GetLocalNow().DateTime + delay;

        (int, int) Range(AccountSettingEnums min, AccountSettingEnums max) =>
            (settingCache.Get(accountId, min), settingCache.Get(accountId, max));
    }
}
