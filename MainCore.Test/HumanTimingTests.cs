using MainCore.Infrasturecture;
using MainCore.Services;
using MainCore.Shared.Enums;

namespace MainCore.Test;

public sealed class HumanTimingTests
{
    [Fact]
    public void SkewedDelay_StaysInRange_ExceptRareLongPauses()
    {
        var random = new Random(7);
        var values = Enumerable.Range(0, 20_000).Select(_ => HumanTiming.SkewedDelay(random, 500, 900)).ToList();

        values.ShouldAllBe(x => x >= 500 && x <= 900 * 5);

        var longPauses = values.Count(x => x > 900);
        longPauses.ShouldBeGreaterThan(0);
        ((double)longPauses / values.Count).ShouldBeLessThan(0.06);
    }

    [Fact]
    public void SkewedDelay_LeansTowardsTheLowEnd()
    {
        var random = new Random(7);
        var inRange = Enumerable.Range(0, 20_000)
            .Select(_ => HumanTiming.SkewedDelay(random, 0, 1000))
            .Where(x => x <= 1000)
            .Order()
            .ToList();

        var median = inRange[inRange.Count / 2];
        median.ShouldBeInRange(250, 450);
        inRange.Count(x => x > 700).ShouldBeGreaterThan(inRange.Count / 50);
    }

    [Fact]
    public void SkewedDelay_HandlesEqualAndSwappedBounds()
    {
        var random = new Random(1);
        HumanTiming.SkewedDelay(random, 0, 0).ShouldBe(0);
        Enumerable.Range(0, 100).Select(_ => HumanTiming.SkewedDelay(random, 900, 500)).ShouldAllBe(x => x >= 500);
    }

    [Fact]
    public void PointInBox_StaysAwayFromEdges_AndIsNotAlwaysTheCentre()
    {
        var random = new Random(3);
        var points = Enumerable.Range(0, 1000).Select(_ => HumanTiming.PointInBox(random, 100, 40)).ToList();

        points.ShouldAllBe(p => p.X >= 20 && p.X <= 80 && p.Y >= 8 && p.Y <= 32);
        points.Select(p => p.X).Distinct().Count().ShouldBeGreaterThan(100);
    }

    [Fact]
    public void RandomizedDefaults_DifferBetweenAccounts_AndKeepValidRanges()
    {
        var random = new Random(5);
        var accounts = Enumerable.Range(0, 50).Select(_ => HumanTiming.RandomizedAccountDefaults(random)).ToList();

        foreach (var settings in accounts)
        {
            settings.Keys.ShouldBe(AppDbContext.AccountDefaultSettings.Keys, ignoreOrder: true);
            settings[AccountSettingEnums.ClickDelayMin].ShouldBeLessThan(settings[AccountSettingEnums.ClickDelayMax]);
            settings[AccountSettingEnums.TaskDelayMin].ShouldBeLessThan(settings[AccountSettingEnums.TaskDelayMax]);
            settings[AccountSettingEnums.FarmIntervalMin].ShouldBeLessThan(settings[AccountSettingEnums.FarmIntervalMax]);
            settings[AccountSettingEnums.WorkTimeMin].ShouldBeLessThan(settings[AccountSettingEnums.WorkTimeMax]);
            (settings[AccountSettingEnums.WorkTimeMin] % 60).ShouldBe(0, "work time is shown in whole hours");
            settings[AccountSettingEnums.NightSleepStart].ShouldBeInRange(0, 24 * 60 - 1);
            settings[AccountSettingEnums.FarmIdleMin].ShouldBe(9);
            settings[AccountSettingEnums.FarmIdleMax].ShouldBe(63);
        }

        accounts.Select(x => x[AccountSettingEnums.ClickDelayMin]).Distinct().Count().ShouldBeGreaterThan(20);
    }

    [Fact]
    public void FarmBurst_SendsInBursts_ThenIdles()
    {
        var random = new Random(11);
        var burst = new FarmBurst();

        // A fixed 60 s farm interval makes interval delays easy to tell from idle gaps (9-63 min).
        var delays = Enumerable.Range(0, 500)
            .Select(_ => burst.NextDelay(random, (60, 60), (9, 63)))
            .ToList();

        var idles = delays.Where(x => x != TimeSpan.FromSeconds(60)).ToList();
        idles.Count.ShouldBeInRange(500 / FarmBurst.MaxBurstSize, 500 / FarmBurst.MinBurstSize + 1);
        idles.ShouldAllBe(x => x >= TimeSpan.FromMinutes(9) && x <= TimeSpan.FromMinutes(63));
        idles.Distinct().Count().ShouldBeGreaterThan(20);

        // A burst of n sends has n - 1 interval delays before its idle gap.
        var run = 0;
        var runs = new List<int>();
        foreach (var delay in delays.SkipWhile(x => x == TimeSpan.FromSeconds(60)).Skip(1))
        {
            if (delay == TimeSpan.FromSeconds(60)) { run++; continue; }
            runs.Add(run);
            run = 0;
        }
        runs.ShouldAllBe(x => x >= FarmBurst.MinBurstSize - 1 && x <= FarmBurst.MaxBurstSize - 1);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36", 131)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0", null)]
    [InlineData("", null)]
    public void UserAgent_ChromeMajorVersion(string userAgent, int? expected)
    {
        UserAgentCheck.ChromeMajorVersion(userAgent).ShouldBe(expected);
    }

    [Fact]
    public void UserAgent_Matches_ComparesMajorVersions()
    {
        const string ua = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        UserAgentCheck.Matches(ua, "131.0.6778.85").ShouldBeTrue();
        UserAgentCheck.Matches(ua, "142").ShouldBeFalse();
        UserAgentCheck.Matches(ua, null).ShouldBeTrue();
        UserAgentCheck.Matches("", "142").ShouldBeTrue();
    }
}
