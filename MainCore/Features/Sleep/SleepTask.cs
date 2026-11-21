using MainCore.Shared.Commands;

namespace MainCore.Features.Sleep;

[Handler]
public sealed partial class SleepTask(
    ILogger logger,
    ChromeManager chromeManager,
    TaskManager taskManager,
    SleepPlanner sleepPlanner,
    INetworkProbe networkProbe,
    OpenBrowserCommand.Handler openBrowserCommand,
    CloseBrowserCommand.Handler closeBrowserCommand,
    SleepCommand.Handler sleepCommand)
{
    public sealed class Task(AccountId accountId) : AccountTask(accountId)
    {
        protected override string TaskName => nameof(SleepTask).Humanize();

        /// <summary>
        /// When the current sleep ends; null while the account is awake. Kept on the task so a retry, or a Resume
        /// after the wake-up failed, picks up where it stopped instead of starting a whole new sleep.
        /// </summary>
        public DateTime? WakeAt { get; set; }
    }

    private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
    {
        var browser = chromeManager.Get(task.AccountId);

        if (task.WakeAt is null && sleepPlanner.GetWorkEndAction(task.AccountId) == WorkEndActionEnums.LogoutAndStop)
        {
            await LogoutFromTravian(browser);
            await closeBrowserCommand.HandleAsync(new(browser), cancellationToken);
            taskManager.SetStatus(task.AccountId, StatusEnums.Offline);
            logger.Information("Work time is over. Logged out and stopped; press Login to start again");
            return Skip.Error;
        }

        if (task.WakeAt is null)
        {
            await closeBrowserCommand.HandleAsync(new(browser), cancellationToken);
            task.WakeAt = sleepPlanner.SleepEnd(task.AccountId);
        }

        var result = await sleepCommand.HandleAsync(new(task.WakeAt.Value), cancellationToken);
        if (result.IsFailed) return result;

        if (!browser.IsInitialized)
        {
            // Opening offline would fall back to this computer's time zone and land on an error page.
            // Failing here lets the scheduler wait for the internet and run this task again, without a new sleep.
            if (!await networkProbe.IsOnlineAsync(task.AccountId, cancellationToken))
            {
                logger.Warning("Time to wake up, but there is no internet to open Chrome");
                return Retry.Error;
            }

            result = await openBrowserCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed) return result;
        }

        task.WakeAt = null;
        return ReSchedule.Error(sleepPlanner.NextSleepStart(task.AccountId));
    }

    // Best effort: end the session on Travian's side like a player would. Closing the browser follows either way.
    private async System.Threading.Tasks.Task LogoutFromTravian(IChromeBrowser browser)
    {
        if (!browser.IsInitialized) return;

        try
        {
            var logoutLink = browser.CurrentPage.Locator("a[href*='logout']").First;
            if (await logoutLink.CountAsync() == 0)
            {
                logger.Warning("Logout link not found, closing the browser without logging out");
                return;
            }

            var result = await browser.Click(logoutLink);
            if (result.IsFailed)
            {
                logger.Warning("Clicking logout failed, closing the browser anyway");
                return;
            }

            await browser.CurrentPage.WaitForLoadStateAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Logout failed, closing the browser anyway: {Message}", ex.Message);
        }
    }
}
