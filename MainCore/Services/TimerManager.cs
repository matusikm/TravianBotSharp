using System.Collections.Concurrent;

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

    /// <summary>
    /// Longest the loop sleeps without a reason to wake. Task and status changes wake it at once,
    /// and the next due task sets the wait, so this only guards against missed signals.
    /// </summary>
    public static readonly TimeSpan MaxIdleWait = TimeSpan.FromSeconds(30);

    private readonly TaskManager _taskManager;
    private readonly RxQueue _rxQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AccountSettingCache _settingCache;
    private readonly ChromeManager _chromeManager;
    private readonly TimeProvider _timeProvider;

    private static readonly ResiliencePropertyKey<ContextData> contextDataKey = new(nameof(ContextData));
    private readonly ResiliencePipeline<Result> _pipeline;

    public TimerManager(TaskManager taskManager, IServiceScopeFactory serviceScopeFactory, RxQueue rxQueue, AccountSettingCache settingCache, ChromeManager chromeManager, TimeProvider timeProvider)
    {
        _taskManager = taskManager;
        _serviceScopeFactory = serviceScopeFactory;
        _rxQueue = rxQueue;
        _settingCache = settingCache;
        _chromeManager = chromeManager;
        _timeProvider = timeProvider;

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

        _pipeline = new ResiliencePipelineBuilder<Result> { TimeProvider = timeProvider }
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

        if (task.ExecuteAt > Now) return;

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
                    async (ctx, state) => Outcome.FromResult(await TaskDispatcher.Execute(scope.ServiceProvider, state, ctx.CancellationToken)),
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

        var delay = _settingCache.Skewed(accountId, AccountSettingEnums.TaskDelayMin, AccountSettingEnums.TaskDelayMax);
        await Task.Delay(TimeSpan.FromMilliseconds(delay), _timeProvider);
    }

    /// <summary>Local time, as task due times are stored in local time.</summary>
    private DateTime Now => _timeProvider.GetLocalNow().DateTime;

    /// <summary>
    /// Sleeps until the next task is due, or until tasks or status change, whichever comes first.
    /// <paramref name="changed"/> is taken before the last check so no change can slip in between.
    /// </summary>
    private async Task WaitForWork(AccountSchedulerActor actor, Task changed, CancellationToken cancellationToken)
    {
        var delay = MaxIdleWait;
        if (actor.Status == StatusEnums.Online)
        {
            var nextDue = actor.Tasks.Select(x => (DateTime?)x.ExecuteAt).Min();
            if (nextDue is not null)
            {
                var untilDue = nextDue.Value - Now;
                if (untilDue <= TimeSpan.Zero) return;
                if (untilDue < delay) delay = untilDue;
            }
        }

        try
        {
            await changed.WaitAsync(delay, _timeProvider, cancellationToken);
        }
        catch (TimeoutException)
        {
            // Next task is due (or the idle cap passed).
        }
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

        _ = Task.Run(() => RunLoop(accountId, cancellationTokenSource), CancellationToken.None);
    }

    private async Task RunLoop(AccountId accountId, CancellationTokenSource cancellationTokenSource)
    {
        var token = cancellationTokenSource.Token;
        var actor = _taskManager.GetActor(accountId);
        try
        {
            while (!_isShutdown && !token.IsCancellationRequested)
            {
                var changed = actor.WaitForChangeAsync();
                try
                {
                    await Execute(accountId);
                    await WaitForWork(actor, changed, token);
                }
                catch (OperationCanceledException) when (_isShutdown || token.IsCancellationRequested)
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
        }
        finally
        {
            if (_loops.TryRemove(new KeyValuePair<AccountId, CancellationTokenSource>(accountId, cancellationTokenSource)))
            {
                cancellationTokenSource.Dispose();
            }
        }
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
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider);
        }
        catch
        {
            // Logging or status updates failing must not kill the loop either.
        }
    }

    public record ContextData(string TaskName, IChromeBrowser Browser);
}
