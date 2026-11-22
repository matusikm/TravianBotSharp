namespace MainCore.Features.ClaimDailyQuest;

[Handler]
public static partial class OpenDailQuestPanelCommand
{
    public sealed record Command(IChromeBrowser Browser) : ICommand
    {
        public string Name => nameof(OpenDailQuestPanelCommand).Humanize();
    }

    private static async ValueTask<Result> HandleAsync(Command command)
    {
        var browser = command.Browser;
        Result result;
        var dailyQuestButton = browser.CurrentPage.GetDailyQuestButton();
        result = await browser.Click(dailyQuestButton);
        if (result.IsFailed) return result;

        // The panel lists many quests; waiting on the whole list breaks Playwright's strict mode,
        // so wait for the first one to show up.
        var dailyQuestItems = browser.CurrentPage.GetDailyQuestItems();
        result = await browser.Wait(dailyQuestItems.First);
        if (result.IsFailed) return result;

        return Result.Ok();
    }
}