using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Services;
using MainCore.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace MainCore.Test;

public sealed class SleepScheduleTests
{
    // 23:30 to 07:00, no jitter unless a test sets it.
    private static readonly NightWindow Night = new(23 * 60 + 30, 7 * 60, 0);
    private static readonly DateTime Day = new(2026, 11, 21);

    [Theory]
    [InlineData(23, 30, true)]
    [InlineData(2, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(12, 0, false)]
    [InlineData(23, 29, false)]
    public void NightWindow_ContainsTimesAcrossMidnight(int hour, int minute, bool expected)
    {
        Night.Contains(new TimeOnly(hour, minute)).ShouldBe(expected);
    }

    [Fact]
    public void NightWindow_ThatDoesNotCrossMidnight()
    {
        var afternoon = new NightWindow(13 * 60, 15 * 60, 0);
        afternoon.Contains(new TimeOnly(14, 0)).ShouldBeTrue();
        afternoon.Contains(new TimeOnly(16, 0)).ShouldBeFalse();
    }

    [Fact]
    public void WorkEndsAtNight_WhenNightComesFirst()
    {
        var now = Day.AddHours(18);
        var start = SleepSchedule.NextSleepStart(now, TimeSpan.FromHours(10), Night, new Random(1));
        start.ShouldBe(Day.AddHours(23).AddMinutes(30));
    }

    [Fact]
    public void WorkEndsAfterWorkTime_WhenThatComesFirst()
    {
        var now = Day.AddHours(8);
        var start = SleepSchedule.NextSleepStart(now, TimeSpan.FromHours(10), Night, new Random(1));
        start.ShouldBe(Day.AddHours(18));
    }

    [Fact]
    public void StartingAtNightByHand_WorksUntilTheNextNight_OrWorkTime()
    {
        var now = Day.AddHours(1);
        var start = SleepSchedule.NextSleepStart(now, TimeSpan.FromHours(10), Night, new Random(1));
        start.ShouldBe(Day.AddHours(11));
    }

    [Fact]
    public void WithoutNightWindow_ItIsPlainWorkTime()
    {
        var now = Day.AddHours(20);
        SleepSchedule.NextSleepStart(now, TimeSpan.FromHours(10), null, new Random(1)).ShouldBe(Day.AddHours(30));
        SleepSchedule.SleepEnd(now, TimeSpan.FromHours(8), null, new Random(1)).ShouldBe(Day.AddHours(28));
    }

    [Fact]
    public void SleepAtNight_LastsUntilMorning()
    {
        var end = SleepSchedule.SleepEnd(Day.AddHours(23).AddMinutes(30), TimeSpan.FromHours(2), Night, new Random(1));
        end.ShouldBe(Day.AddDays(1).AddHours(7));
    }

    [Fact]
    public void DaytimeSleep_ThatWouldWakeAtNight_IsExtendedToMorning()
    {
        // 20:00 + 8 h = 04:00, inside the night: keep sleeping until 07:00.
        var end = SleepSchedule.SleepEnd(Day.AddHours(20), TimeSpan.FromHours(8), Night, new Random(1));
        end.ShouldBe(Day.AddDays(1).AddHours(7));
    }

    [Fact]
    public void DaytimeSleep_ThatEndsInDaytime_IsUnchanged()
    {
        var end = SleepSchedule.SleepEnd(Day.AddHours(9), TimeSpan.FromHours(2), Night, new Random(1));
        end.ShouldBe(Day.AddHours(11));
    }

    [Fact]
    public void Jitter_ShiftsTheEdges_WithinTheLimit_AndVaries()
    {
        var night = Night with { Jitter = 45 };
        var random = new Random(42);
        var starts = Enumerable.Range(0, 200)
            .Select(_ => SleepSchedule.NextSleepStart(Day.AddHours(18), TimeSpan.FromHours(12), night, random))
            .ToList();

        var expected = Day.AddHours(23).AddMinutes(30);
        starts.ShouldAllBe(x => Math.Abs((x - expected).TotalMinutes) <= 45);
        starts.Distinct().Count().ShouldBeGreaterThan(20);
    }

    [Fact]
    public void Planner_UsesTheAccountsTimeZone_AndReturnsMachineTime()
    {
        // Machine in Prague (UTC+1 in November), account in Bangkok (UTC+7).
        // 18:00 in Prague is 00:00 in Bangkok, inside the night: the account should sleep until 07:00 Bangkok,
        // which is 01:00 in Prague.
        var prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
        var bangkok = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 17, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(prague);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using (var context = new AppDbContext(options))
        {
            context.Database.EnsureCreated();
            context.Accounts.Add(new Account
            {
                Id = 1,
                Username = "player",
                Server = "https://ts1.x1.asia.travian.com",
                Settings = [.. AppDbContext.AccountDefaultSettings.Select(x => new AccountSetting
                {
                    Setting = x.Key,
                    Value = x.Key == AccountSettingEnums.NightSleepJitter ? 0 : x.Value,
                })],
            });
            context.SaveChanges();
        }

        var factory = Substitute.For<IDbContextFactory<AppDbContext>>();
        factory.CreateDbContext().Returns(_ => new AppDbContext(options));

        var account = new AccountId(1);
        var zones = new AccountTimeZoneStore(time);
        zones.Set(account, bangkok);
        var planner = new SleepPlanner(new AccountSettingCache(factory), zones, time);

        planner.SleepEnd(account).ShouldBe(new DateTime(2026, 11, 22, 1, 0, 0));

        // Started by hand at midnight Bangkok: work 10-12 h (default), ending 10:00-12:00 Bangkok = 04:00-06:00 Prague.
        var nextSleep = planner.NextSleepStart(account);
        nextSleep.ShouldBeInRange(new DateTime(2026, 11, 22, 4, 0, 0), new DateTime(2026, 11, 22, 6, 0, 0));
    }
}
