using MainCore.Features.Sleep;
using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Services;
using MainCore.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MainCore.Test;

public class SchedulerTests
{
    private static readonly AccountId Account = new(1);

    private static RxQueue CreateRxQueue() => new(Substitute.For<IDbContextFactory<AppDbContext>>());

    [Fact]
    public async Task TaskList_SurvivesConcurrentAddRemoveAndRead()
    {
        var actor = new AccountSchedulerActor(Account, CreateRxQueue());

        var writers = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            for (var n = 0; n < 2_000; n++)
            {
                var task = new SleepTask.Task(new AccountId(i));
                actor.Add(task, first: n % 3 == 0);
                actor.Remove(task);
            }
        }));
        var readers = Enumerable.Range(0, 4).Select(r => Task.Run(() =>
        {
            for (var n = 0; n < 2_000; n++)
            {
                _ = actor.Tasks.OrderBy(x => x.ExecuteAt).ToList();
            }
        }));

        await Task.WhenAll(writers.Concat(readers));

        actor.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public void Tasks_ReturnsSnapshot()
    {
        var actor = new AccountSchedulerActor(Account, CreateRxQueue());
        actor.Add(new SleepTask.Task(Account), first: false);

        var snapshot = actor.Tasks;
        actor.Clear();

        snapshot.Count.ShouldBe(1);
        actor.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task TaskManager_AddsSameTaskOnlyOnce_WhenCalledConcurrently()
    {
        var taskManager = new TaskManager(CreateRxQueue());

        await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => taskManager.Add(new SleepTask.Task(Account)))));

        taskManager.GetTaskList(Account).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Loop_KeepsRunningAndPausesAccount_WhenTaskThrows()
    {
        var contextFactory = Substitute.For<IDbContextFactory<AppDbContext>>();
        contextFactory.CreateDbContext().Returns(_ => throw new InvalidOperationException("database is gone"));

        var rxQueue = CreateRxQueue();
        var taskManager = new TaskManager(rxQueue);
        var settingCache = new AccountSettingCache(contextFactory);
        var timerManager = new TimerManager(
            taskManager,
            Substitute.For<IServiceScopeFactory>(),
            rxQueue,
            settingCache,
            new ChromeManager(contextFactory, settingCache),
            TimeProvider.System);

        var task = new SleepTask.Task(Account) { ExecuteAt = DateTime.Now.AddSeconds(-1) };
        taskManager.Add(task);
        taskManager.SetStatus(Account, StatusEnums.Online);

        timerManager.Start(Account);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (taskManager.GetStatus(Account) != StatusEnums.Paused && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            taskManager.GetStatus(Account).ShouldBe(StatusEnums.Paused);
            task.Stage.ShouldBe(StageEnums.Waiting);
            taskManager.GetActor(Account).IsExecuting.ShouldBeFalse();
            timerManager.IsRunning(Account).ShouldBeTrue();
        }
        finally
        {
            timerManager.Shutdown();
        }
    }
}
