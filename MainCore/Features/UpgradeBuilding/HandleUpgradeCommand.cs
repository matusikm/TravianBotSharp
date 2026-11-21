namespace MainCore.Features.UpgradeBuilding;

[Handler]
public sealed partial class UpgradeCommand(ILogger logger)
{
    public sealed record Command(IChromeBrowser Browser, NormalBuildPlan Plan, bool CanUseSpecialUpgrade, TimeSpan SpecialUpgradeMinTime = default) : ICommand
    {
        public string Name => $"{nameof(UpgradeCommand).Humanize()} [{Description}]";
        private string Description => $"{Plan}";
    }

    private async ValueTask<Result> HandleAsync(Command command, CancellationToken cancellationToken)
    {
        var (browser, plan, canUseSpecialUpgrade, minTime) = command;

        if (canUseSpecialUpgrade && !UnskippableBuildings.Contains(plan.Type)
            && await ShouldUseSpecialUpgrade(browser, plan.Type, minTime))
        {
            var special = await SpecialUpgrade(browser, plan.Type, cancellationToken);
            if (special.IsSuccess) return Result.Ok();
            // Only page problems (missing video, timeouts) fall back. No page or a cancel stops as before.
            if (!special.HasError<Retry>()) return special;

            logger.Warning("Video upgrade did not work, using the normal upgrade button instead.");
            if (await browser.CurrentPage.GetDorf(plan.Type).CountAsync() > 0)
            {
                // The video finished late and Travian already went back to the village.
                return Result.Ok();
            }

            var refresh = await browser.Refresh();
            if (refresh.IsFailed) return refresh;
        }

        return await Upgrade(browser, plan.Type);
    }

    private static readonly List<BuildingEnums> UnskippableBuildings =
    [
        BuildingEnums.Residence,
        BuildingEnums.Palace,
        BuildingEnums.CommandCenter,
    ];

    private async Task<bool> ShouldUseSpecialUpgrade(IChromeBrowser browser, BuildingEnums building, TimeSpan minTime)
    {
        var button = await browser.CurrentPage.GetSpecialButton(building);
        if (await button.CountAsync() == 0)
        {
            logger.Information("No video upgrade button on this page, using the normal upgrade button.");
            return false;
        }

        if (minTime <= TimeSpan.Zero) return true;

        var duration = await browser.CurrentPage.GetUpgradeDuration(building);
        if (duration is null)
        {
            logger.Warning("Could not read the build time, using the normal upgrade button.");
            return false;
        }

        if (duration < minTime)
        {
            logger.Information("Build time {Duration} is shorter than {MinTime}, using the normal upgrade button.", duration, minTime);
            return false;
        }

        logger.Information("Build time {Duration} is at least {MinTime}, using the video upgrade button.", duration, minTime);
        return true;
    }

    /// <summary>
    /// Clicks the video button and lets the ad play. One attempt only: any page problem returns
    /// <see cref="Retry"/> so the caller can fall back to the normal button.
    /// </summary>
    private static async Task<Result> SpecialUpgrade(IChromeBrowser browser, BuildingEnums building, CancellationToken cancellationToken)
    {
        var page = browser.CurrentPage;
        var button = await page.GetSpecialButton(building);
        var result = await browser.Click(button);
        if (result.IsFailed) return result;

        var videoFeature = page.Locator("#videoFeature");
        await Wait(4_000, 8_000, cancellationToken);
        if (await videoFeature.CountAsync() == 0)
        {
            browser.Logger.Information("Video window not shown.");
            return Retry.Error;
        }

        var classes = await videoFeature.GetAttributeAsync("class") ?? "";
        if (classes.Contains("infoScreen"))
        {
            var checkBoxDontShowAgain = page.Locator("#videoFeature label.checkbox input[name=\"preference\"]");
            result = await browser.Click(checkBoxDontShowAgain);
            if (result.IsFailed) return result;

            var buttonWatchAds = page.Locator("#videoFeature div.buttonWrapper button");
            result = await browser.Click(buttonWatchAds);
            if (result.IsFailed) return result;
        }

        await Wait(9_000, 15_000, cancellationToken);
        if (await page.Locator("#videoArea > *").CountAsync() == 0)
        {
            browser.Logger.Information("Video not loaded.");
            return Retry.Error;
        }

        await Wait(20_000, 32_000, cancellationToken);
        return await browser.Wait(page.GetDorf(building));
    }

    private static Task Wait(int minMs, int maxMs, CancellationToken cancellationToken)
    {
        var delay = HumanTiming.SkewedDelay(Random.Shared, minMs, maxMs);
        return Task.Delay(delay, cancellationToken);
    }

    private static async Task<Result> Upgrade(IChromeBrowser browser, BuildingEnums building)
    {
        var button = await browser.CurrentPage.GetNormalButton(building);
        var result = await browser.Click(button);
        if (result.IsFailed) return result;

        result = await browser.Wait(browser.CurrentPage.GetDorf(building));
        if (result.IsFailed) return result;

        return Result.Ok();
    }
}
