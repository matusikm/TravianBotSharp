using FluentResults;
using Immediate.Handlers.Shared;
using MainCore.Features.Sleep;
using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Services;
using MainCore.Shared.Enums;
using MainCore.Shared.Errors;
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
    private readonly INetworkProbe _networkProbe = Substitute.For<INetworkProbe>();
    private volatile bool _online = true;

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

        _networkProbe.IsOnlineAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_online));

        var rxQueue = new RxQueue(contextFactory);
        var settingCache = new AccountSettingCache(contextFactory);
        _taskManager = new TaskManager(rxQueue);
        _timerManager = new TimerManager(
            _taskManager,
            _services.GetRequiredService<IServiceScopeFactory>(),
            rxQueue,
            settingCache,
            new ChromeManager(contextFactory, settingCache),
            _time,
            _networkProbe);
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

        // The loop removes the task after the handler returns, so wait for that instead of racing it.
        await WaitUntil(() => _taskManager.GetTaskList(Account).Count == 0);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(RealTimeout);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
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

    [Fact]
    public async Task WaitsForNetwork_WhenTaskFailsOffline_AndKeepsTheTask()
    {
        _online = false;
        _handler.Response = Retry.Error;
        var task = new SleepTask.Task(Account) { ExecuteAt = LocalNow };
        _taskManager.Add(task);
        _taskManager.SetStatus(Account, StatusEnums.Online);

        _timerManager.Start(Account);

        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.WaitingForNetwork);
        _handler.Calls.ShouldBe(1); // no retries while offline
        _taskManager.GetTaskList(Account).ShouldContain(task);
    }

    [Fact]
    public async Task WaitsForNetwork_WhenStopIsCausedByMissingInternet()
    {
        _online = false;
        _handler.Response = Stop.Error;
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });
        _taskManager.SetStatus(Account, StatusEnums.Online);

        _timerManager.Start(Account);

        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.WaitingForNetwork);
    }

    [Fact]
    public async Task Pauses_OnStop_WhenInternetWorks()
    {
        _handler.Response = Stop.Error;
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });
        _taskManager.SetStatus(Account, StatusEnums.Online);

        _timerManager.Start(Account);

        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.Paused);
    }

    [Fact]
    public async Task ResumesAndRunsTaskAgain_WhenInternetIsBack()
    {
        _online = false;
        _handler.Response = Retry.Error;
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });
        _taskManager.SetStatus(Account, StatusEnums.Online);
        _timerManager.Start(Account);
        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.WaitingForNetwork);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        _online = true;
        _handler.Response = Result.Ok();
        _time.Advance(TimerManager.NetworkCheckInterval);

        await WaitUntil(() => _handler.Calls == 2);
        await WaitUntil(() => _taskManager.GetTaskList(Account).Count == 0);
        _taskManager.GetStatus(Account).ShouldBe(StatusEnums.Online);
    }

    [Fact]
    public async Task Pauses_WhenInternetStaysDownTooLong()
    {
        _online = false;
        _handler.Response = Retry.Error;
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });
        _taskManager.SetStatus(Account, StatusEnums.Online);
        _timerManager.Start(Account);
        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.WaitingForNetwork);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        _time.Advance(TimerManager.MaxNetworkWait);

        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.Paused);
        _handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task UserPause_WhileWaitingForNetwork_IsNotUndone()
    {
        _online = false;
        _handler.Response = Retry.Error;
        _taskManager.Add(new SleepTask.Task(Account) { ExecuteAt = LocalNow });
        _taskManager.SetStatus(Account, StatusEnums.Online);
        _timerManager.Start(Account);
        await WaitUntil(() => _taskManager.GetStatus(Account) == StatusEnums.WaitingForNetwork);

        await _taskManager.StopCurrentTask(Account);
        _online = true;
        _time.Advance(TimerManager.NetworkCheckInterval);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        _taskManager.GetStatus(Account).ShouldBe(StatusEnums.Paused);
        _handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshBeforeRetry_SwallowsReloadErrors()
    {
        var browser = Substitute.For<IChromeBrowser>();
        browser.Logger.Returns(Serilog.Core.Logger.None);
        browser.Refresh().Returns<Task<Result>>(_ => throw new Microsoft.Playwright.PlaywrightException("net::ERR_INTERNET_DISCONNECTED"));

        await Should.NotThrowAsync(() => TimerManager.RefreshBeforeRetry(browser));
    }

    private sealed class RecordingHandler : IHandler<SleepTask.Task, Result>
    {
        private int _calls;

        public TaskCompletionSource<SleepTask.Task> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Result Response { get; set; } = Result.Ok();

        public int Calls => Volatile.Read(ref _calls);

        public ValueTask<Result> HandleAsync(SleepTask.Task request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            Called.TrySetResult(request);
            return ValueTask.FromResult(Response);
        }
    }
}
