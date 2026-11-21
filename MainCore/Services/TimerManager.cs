using System.Collections.Concurrent;

using MainCore.Features.ClaimDailyQuest;
using MainCore.Features.ClaimVillageQuest;
using MainCore.Features.InstantUpgrade;
using MainCore.Features.Login;
using MainCore.Features.NPC;
using MainCore.Features.SetHeroPoint;
using MainCore.Features.Sleep;
using MainCore.Features.StartAdventure;
using MainCore.Features.StartFarmList;
using MainCore.Features.TrainTroop;
using MainCore.Features.Update;
using MainCore.Features.UpgradeBuilding;

using Microsoft.Extensions.DependencyInjection;

using Polly;
using Polly.Retry;

using Serilog;

namespace MainCore.Services;

[RegisterSingleton<TimerManager>]
public sealed class TimerManager
{
    private readonly ConcurrentDictionary<AccountId, CancellationTokenSource> _loops = [];

    private volatile bool _isShutdown = false;

    private readonly TaskManager _taskManager;
    private readonly RxQueue _rxQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ChromeManager _chromeManager;

    private static readonly ResiliencePropertyKey<ContextData> contextDataKey = new(nameof(ContextData));
    private readonly ResiliencePipeline<Result> _pipeline;

    public TimerManager(TaskManager taskManager, IServiceScopeFactory serviceScopeFactory, RxQueue rxQueue, IDbContextFactory<AppDbContext> contextFactory, ChromeManager chromeManager)
    {
        _taskManager = taskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _rxQueue = rxQueue;
        _contextFactory = contextFactory;
        _chromeManager = chromeManager;

        static async ValueTask OnRetry(OnRetryArguments<Result> args)
        {
            if (!args.Context.Properties.TryGetValue(contextDataKey, out var contextData)) return;

            var (taskName, browser) = contextData;
            var error = args.Outcome;
            if (error.Exception is not null)
            {
                var exception = error.Exception;
                browser.Logger.Error(exception, "{Message}", exception.Message);
            }
            if (error.Result is not null)
            {
                var message = string.Join(Environment.NewLine, error.Result.Reasons.Select(e => e.Message));
                if (!string.IsNullOrEmpty(message))
                {
                    browser.Logger.Warning("Task {TaskName} failed: {Message}", taskName, message);
                }
            }
            await browser.Refresh();
            browser.Logger.Warning("{TaskName} will retry after {RetryDelay} (#{AttemptNumber} times)", taskName, args.RetryDelay, args.AttemptNumber + 1);
        }

        var retryOptions = new RetryStrategyOptions<Result>()
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(30),
            UseJitter = true,
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder<Result>()
               .Handle<Exception>()
               .HandleResult(static x => x.HasError<Retry>()),
            OnRetry = OnRetry
        };

        _pipeline = new ResiliencePipelineBuilder<Result>()
            .AddRetry(retryOptions)
            .Build();
    }

    public async Task Execute(AccountId accountId)
    {
        var actor = _taskManager.GetActor(accountId);

        var status = actor.Status;
        if (status != StatusEnums.Online) return;
        var tasks = actor.Tasks.OrderBy(x => x.ExecuteAt).ToList();
        if (tasks.Count == 0) return;
        var task = tasks[0];

        if (task.ExecuteAt > DateTime.Now) return;

        actor.IsExecuting = true;
        var cts = new CancellationTokenSource();
        actor.CancellationTokenSource = cts;

        task.Stage = StageEnums.Executing;
        _rxQueue.Enqueue(new TasksModified(accountId));

        IChromeBrowser browser;
        Outcome<Result> poliResult;
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();

            ///===========================================================///
            browser = _chromeManager.Get(accountId);

            var contextData = new ContextData(task.Description, browser);

            ///===========================================================///
            var resilienceContext = ResilienceContextPool.Shared.Get(cts.Token);
            try
            {
                resilienceContext.Properties.Set(contextDataKey, contextData);

                poliResult = await _pipeline.ExecuteOutcomeAsync(
                    async (ctx, state) => Outcome.FromResult(await scope.Execute(state, ctx.CancellationToken)),
                    resilienceContext,
                    task);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(resilienceContext);
            }
            ///===========================================================///
        }
        finally
        {
            // Always hand the task back, or StopCurrentTask waits forever for it.
            task.Stage = StageEnums.Waiting;
            _rxQueue.Enqueue(new TasksModified(accountId));

            actor.IsExecuting = false;

            actor.CancellationTokenSource = null;
            cts.Dispose();
        }

        if (poliResult.Exception is not null)
        {
            var ex = poliResult.Exception;

            if (ex is OperationCanceledException)
            {
                browser.Logger.Information("Pause button is pressed");
            }
            else
            {
                var filename = await browser.Screenshot();
                browser.Logger.Information("Screenshot saved as {FileName}", filename);
                browser.Logger.Warning("There is something wrong. Bot is pausing. Last exception is");
                browser.Logger.Error(ex, "{Message}", ex.Message);
            }

            _taskManager.SetStatus(accountId, StatusEnums.Paused);
        }
        else if (poliResult.Result is not null)
        {
            var result = poliResult.Result;
            if (result.IsFailed)
            {
                if (result.HasError<Stop>() || result.HasError<Retry>())
                {
                    var filename = await browser.Screenshot();
                    browser.Logger.Information(messageTemplate: "Screenshot saved as {FileName}", filename);
                    _taskManager.SetStatus(accountId, StatusEnums.Paused);
                }
                else if (result.HasError<Skip>())
                {
                    _taskManager.Remove(accountId, task);
                }
                else if (result.HasError<ReSchedule>())
                {
                    var rescheduleError = result.Errors.OfType<ReSchedule>().First();
                    task.ExecuteAt = rescheduleError.ExecuteAt;
                }
                else if (result.HasError<Cancel>())
                {
                    _taskManager.SetStatus(accountId, StatusEnums.Paused);
                }
            }
            else
            {
                _taskManager.Remove(accountId, task);
            }
        }
        else
        {
            _taskManager.Remove(accountId, task);
        }

        using var context = _contextFactory.CreateDbContext();
        var delay = context.ByName(accountId, AccountSettingEnums.TaskDelayMin, AccountSettingEnums.TaskDelayMax);
        await Task.Delay(delay);
    }

    public void Shutdown()
    {
        _isShutdown = true;

        foreach (var accountId in _loops.Keys)
        {
            if (_loops.TryRemove(accountId, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
        }
    }

    public void Start(AccountId accountId)
    {
        if (_isShutdown) return;

        var cancellationTokenSource = new CancellationTokenSource();
        if (!_loops.TryAdd(accountId, cancellationTokenSource))
        {
            cancellationTokenSource.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            while (!_isShutdown && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Execute(accountId);
                    await Task.Delay(100, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) when (_isShutdown || cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // An unexpected error must not end the loop: the account would look Online
                    // but never run again, and Start() would refuse to restart it.
                    await HandleLoopError(accountId, ex);
                }
            }

            if (_loops.TryRemove(accountId, out var ownedCancellationTokenSource))
            {
                ownedCancellationTokenSource.Dispose();
            }
        }, cancellationTokenSource.Token);
    }

    /// <summary>True while the account's scheduler loop is running.</summary>
    public bool IsRunning(AccountId accountId) => _loops.ContainsKey(accountId);

    private async Task HandleLoopError(AccountId accountId, Exception ex)
    {
        try
        {
            Log.ForContext("AccountId", accountId)
                .Error(ex, "Unexpected error in the task loop. Bot is pausing: {Message}", ex.Message);
            _taskManager.SetStatus(accountId, StatusEnums.Paused);
            await Task.Delay(1000);
        }
        catch
        {
            // Logging or status updates failing must not kill the loop either.
        }
    }

    public record ContextData(string TaskName, IChromeBrowser Browser);
}

public static class ServiceScopeExtension
{
    private static IHandler<T, Result> GetHandler<T>(this IServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<IHandler<T, Result>>();
    }

    public static async Task<Result> Execute(this IServiceScope scope, BaseTask task, CancellationToken cancellationToken)
    {
        switch (task)
        {
            case ClaimVillageQuestTask.Task claimQuestTask:
                var claimQuestTaskHandler = scope.GetHandler<ClaimVillageQuestTask.Task>();
                return await claimQuestTaskHandler.HandleAsync(claimQuestTask, cancellationToken);

            case InstantUpgradeTask.Task completeImmediatelyTask:
                var completeImmediatelyTaskHandler = scope.GetHandler<InstantUpgradeTask.Task>();
                return await completeImmediatelyTaskHandler.HandleAsync(completeImmediatelyTask, cancellationToken);

            case LoginTask.Task loginTask:
                var loginTaskHandler = scope.GetHandler<LoginTask.Task>();
                return await loginTaskHandler.HandleAsync(loginTask, cancellationToken);

            case NPCTask.Task npcTask:
                var npcTaskHandler = scope.GetHandler<NPCTask.Task>();
                return await npcTaskHandler.HandleAsync(npcTask, cancellationToken);

            case SleepTask.Task sleepTask:
                var sleepTaskHandler = scope.GetHandler<SleepTask.Task>();
                return await sleepTaskHandler.HandleAsync(sleepTask, cancellationToken);

            case StartAdventureTask.Task startAdventureTask:
                var startAdventureTaskHandler = scope.GetHandler<StartAdventureTask.Task>();
                return await startAdventureTaskHandler.HandleAsync(startAdventureTask, cancellationToken);

            case StartFarmListTask.Task startFarmListTask:
                var startFarmListTaskHandler = scope.GetHandler<StartFarmListTask.Task>();
                return await startFarmListTaskHandler.HandleAsync(startFarmListTask, cancellationToken);

            case TrainTroopTask.Task trainTroopTask:
                var trainTroopTaskHandler = scope.GetHandler<TrainTroopTask.Task>();
                return await trainTroopTaskHandler.HandleAsync(trainTroopTask, cancellationToken);

            case UpdateBuildingTask.Task updateBuildingTask:
                var updateBuildingTaskHandler = scope.GetHandler<UpdateBuildingTask.Task>();
                return await updateBuildingTaskHandler.HandleAsync(updateBuildingTask, cancellationToken);

            case UpdateFarmListTask.Task updateFarmListTask:
                var updateFarmListTaskHandler = scope.GetHandler<UpdateFarmListTask.Task>();
                return await updateFarmListTaskHandler.HandleAsync(updateFarmListTask, cancellationToken);

            case UpdateVillageTask.Task updateVillageTask:
                var updateVillageTaskHandler = scope.GetHandler<UpdateVillageTask.Task>();
                return await updateVillageTaskHandler.HandleAsync(updateVillageTask, cancellationToken);

            case UpgradeBuildingTask.Task upgradeBuildingTask:
                var upgradeBuildingTaskHandler = scope.GetHandler<UpgradeBuildingTask.Task>();
                return await upgradeBuildingTaskHandler.HandleAsync(upgradeBuildingTask, cancellationToken);

            case ClaimDailyQuestTask.Task claimDailyQuestTask:
                var claimDailyQuestTaskHandler = scope.GetHandler<ClaimDailyQuestTask.Task>();
                return await claimDailyQuestTaskHandler.HandleAsync(claimDailyQuestTask, cancellationToken);

            case SetHeroPointTask.Task setHeroPointTask:
                var setHeroPointTaskHandler = scope.GetHandler<SetHeroPointTask.Task>();
                return await setHeroPointTaskHandler.HandleAsync(setHeroPointTask, cancellationToken);

            default:
                throw new NotImplementedException($"Task {task.GetType().Name} is not implemented");
        }
    }
}