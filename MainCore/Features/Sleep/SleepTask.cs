using MainCore.Shared.Commands;

namespace MainCore.Features.Sleep;

[Handler]
public sealed partial class SleepTask(
    ILogger logger,
    ChromeManager chromeManager,
    TaskManager taskManager,
    SleepPlanner sleepPlanner,
    OpenBrowserCommand.Handler openBrowserCommand,
    CloseBrowserCommand.Handler closeBrowserCommand,
    SleepCommand.Handler sleepCommand)
{
    public sealed class Task(AccountId accountId) : AccountTask(accountId)
    {
        protected override string TaskName => nameof(SleepTask).Humanize();
    }

    private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
    {
        var browser = chromeManager.Get(task.AccountId);

        if (sleepPlanner.GetWorkEndAction(task.AccountId) == WorkEndActionEnums.LogoutAndStop)
        {
            await LogoutFromTravian(browser);
            await closeBrowserCommand.HandleAsync(new(browser), cancellationToken);
            taskManager.SetStatus(task.AccountId, StatusEnums.Offline);
            logger.Information("Work time is over. Logged out and stopped; press Login to start again");
            return Skip.Error;
        }

        await closeBrowserCommand.HandleAsync(new(browser), cancellationToken);

        var sleepEnd = sleepPlanner.SleepEnd(task.AccountId);
        var result = await sleepCommand.HandleAsync(new(sleepEnd), cancellationToken);
        if (result.IsFailed) return result;

        result = await openBrowserCommand.HandleAsync(new(browser), cancellationToken);
        if (result.IsFailed) return result;

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
