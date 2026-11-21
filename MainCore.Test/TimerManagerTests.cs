using FluentResults;
using Immediate.Handlers.Shared;
using MainCore.Features.Sleep;
using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Services;
using MainCore.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MainCore.Test;

public sealed class TimerManagerTests : IDisposable
{
    private static readonly AccountId Account = new(1);
    private static readonly TimeSpan RealTimeout = TimeSpan.FromSeconds(5);

    private readonly SqliteConnection _connection;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingHandler _handler = new();
    private readonly TaskManager _taskManager;
    private readonly TimerManager _timerManager;
    private readonly ServiceProvider _services;

    public TimerManagerTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using (var context = new AppDbContext(options))
        {
            context.Database.EnsureCreated();
            context.Database.ExecuteSqlRaw("""
                INSERT INTO Accounts (Id, Username, Server) VALUES (1, 'player', 'https://ts1.x1.europe.travian.com');
                INSERT INTO AccountsSetting (AccountId, Setting, Value) VALUES (1, {0}, 0), (1, {1}, 0);
                """, (int)AccountSettingEnums.TaskDelayMin, (int)AccountSettingEnums.TaskDelayMax);
        }

        var contextFactory = Substitute.For<IDbContextFactory<AppDbContext>>();
        contextFactory.CreateDbContext().Returns(_ => new AppDbContext(options));

        _services = new ServiceCollection()
            .AddSingleton<IHandler<SleepTask.Task, Result>>(_handler)
            .BuildServiceProvider();

        var rxQueue = new RxQueue(contextFactory);
        var settingCache = new AccountSettingCache(contextFactory);
        _taskManager = new TaskManager(rxQueue);
        _timerManager = new TimerManager(
            _taskManager,
            _services.GetRequiredService<IServiceScopeFactory>(),
            rxQueue,
            settingCache,
            new ChromeManager(contextFactory, settingCache),
            _time);
    }

    public void Dispose()
    {
        _timerManager.Shutdown();
        _services.Dispose();
        _connection.Dispose();
    }

    private DateTime LocalNow => _time.GetLocalNow().DateTime;

    [Fact]
    public async Task RunsTask_OnlyWhenItIsDue()
    {
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow.AddMinutes(10) });
        _taskManager.SetStatus(Account, StatusEnums.Online);

        _timerManager.Start(Account);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        _handler.Called.Task.IsCompleted.ShouldBeFalse();

        _time.Advance(TimeSpan.FromMinutes(10));

        await _handler.Called.Task.WaitAsync(RealTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WakesUp_WhenTaskIsAdded()
    {
        _taskManager.SetStatus(Account, StatusEnums.Online);
        _timerManager.Start(Account);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        // No fake time passes: only the add can wake the loop.
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });

        await _handler.Called.Task.WaitAsync(RealTimeout, TestContext.Current.CancellationToken);
        _taskManager.GetTaskList(Account).ShouldBeEmpty();
    }

    [Fact]
    public async Task WakesUp_WhenAccountGoesOnline()
    {
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });
        _taskManager.SetStatus(Account, StatusEnums.Paused);
        _timerManager.Start(Account);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        _handler.Called.Task.IsCompleted.ShouldBeFalse();

        _taskManager.SetStatus(Account, StatusEnums.Online);

        await _handler.Called.Task.WaitAsync(RealTimeout, TestContext.Current.CancellationToken);
    }

    private sealed class RecordingHandler : IHandler<SleepTask.Task, Result>
    {
        public TaskCompletionSource<SleepTask.Task> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<Result> HandleAsync(SleepTask.Task request, CancellationToken cancellationToken = default)
        {
            Called.TrySetResult(request);
            return ValueTask.FromResult(Result.Ok());
        }
    }
}
