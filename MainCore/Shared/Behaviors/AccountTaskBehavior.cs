using MainCore.Features.Login;
using MainCore.Features.Sleep;
using MainCore.Features.Update;
using MainCore.Shared.Commands;

namespace MainCore.Shared.Behaviors
{
    public sealed class AccountTaskBehavior<TRequest, TResponse>(
        ILogger logger,
        IDbContextFactory<AppDbContext> contextFactory,
        ChromeManager chromeManager,
        TaskManager taskManager,
        INetworkProbe networkProbe,
        OpenBrowserCommand.Handler openBrowserCommand,
        UpdateAccountInfoCommand.Handler updateAccountInfoCommand,
        UpdateDailyQuestCommand.Handler updateDailyQuestCommand,
        UpdateHeroLevelUpCommand.Handler updateHeroLevelUpCommand,
        UpdateAdventureCommand.Handler updateAdventureCommand)
            : Behavior<TRequest, TResponse>
                where TRequest : AccountTask
                where TResponse : Result
    {
        public override async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return (TResponse)Cancel.Error;
            }
            var accountId = request.AccountId;
            var cacheExecuteAt = request.ExecuteAt;
            var browser = chromeManager.Get(accountId);

            if (!browser.IsInitialized)
            {
                // The sleep task closes and reopens the browser itself.
                if (request is SleepTask.Task) return await Next(request, cancellationToken);

                // Chrome is closed while the account runs, for example after a wake-up failed without internet.
                var opened = await OpenBrowser(browser, cancellationToken);
                if (opened.IsFailed) return (TResponse)opened;
            }

            var isIngamePage = await browser.CurrentPage.IsIngamePage();
            request.ExecuteAt = cacheExecuteAt;

            if (!isIngamePage && !await browser.CurrentPage.IsLoginPage())
            {
                // Chrome's own "No internet" page after an outage, or a page that never finished loading.
                logger.Warning("The page is neither the game nor the login page. Opening the server again");
                var navigated = await browser.Navigate(GetServer(accountId));
                if (navigated.IsFailed) return (TResponse)navigated;
                isIngamePage = await browser.CurrentPage.IsIngamePage();
            }

            if (!isIngamePage)
            {
                var isLoginPage = await browser.CurrentPage.IsLoginPage();
                if (!isLoginPage)
                {
                    try
                    {
                        await browser.CurrentPage.GetServerTime().WaitForAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Travian is not ingame nor login page. Please check browser. Exception: {Exception}", ex.Message);
                        return (TResponse)Stop.Error;
                    }

                    await updateAccountInfoCommand.HandleAsync(new(browser), cancellationToken);
                }

                if (request is not LoginTask.Task)
                {
                    taskManager.Add<LoginTask.Task>(new(accountId), first: true);
                    request.ExecuteAt = request.ExecuteAt.AddSeconds(1);
                    logger.Information("Account is logout. Re-login now");
                    return (TResponse)Skip.Error;
                }
            }
            else
            {
                await updateAccountInfoCommand.HandleAsync(new(browser), cancellationToken);
            }

            var response = await Next(request, cancellationToken);

            // A task may close the browser (sleep, or log out and stop), so there may be no page left to read.
            isIngamePage = browser.IsInitialized && await browser.CurrentPage.IsIngamePage();
            if (isIngamePage)
            {
                await updateAccountInfoCommand.HandleAsync(new(browser), cancellationToken);
                await updateAdventureCommand.HandleAsync(new(browser), cancellationToken);
                await updateDailyQuestCommand.HandleAsync(new(browser), cancellationToken);
                await updateHeroLevelUpCommand.HandleAsync(new(browser), cancellationToken);
            }

            return response;
        }

        private async Task<Result> OpenBrowser(IChromeBrowser browser, CancellationToken cancellationToken)
        {
            // Opening offline would fall back to this computer's time zone and land on an error page.
            if (!await networkProbe.IsOnlineAsync(browser.AccountId, cancellationToken))
            {
                logger.Warning("Chrome is closed and there is no internet to open it");
                return Retry.Error;
            }

            logger.Information("Chrome is closed. Opening it again");
            return await openBrowserCommand.HandleAsync(new(browser, IgnoreSleepTime: true), cancellationToken);
        }

        private string GetServer(AccountId accountId)
        {
            using var context = contextFactory.CreateDbContext();
            return context.Accounts
                .Where(x => x.Id == accountId.Value)
                .Select(x => x.Server)
                .First();
        }
    }
}