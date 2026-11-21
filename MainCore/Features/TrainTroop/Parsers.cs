using System.Net;

namespace MainCore.Features.TrainTroop;

public static class Parsers
{
    public static ILocator GetTroopRow(this IPage page, TroopEnums troop)
    {
        var node = page.Locator($"div.troop:not(.empty):has(img.unit.u{(int)troop})");
        return node;
    }

    public static ILocator GetInputBox(this IPage page, TroopEnums troop)
    {
        var node = page.GetTroopRow(troop).Locator("div.cta input.text");
        return node;
    }

    /// <summary>
    /// How many of the troop the village can afford now, or null when the building has no training row for it
    /// (the troop isn't researched yet). The row is counted first instead of waited for: reading a missing
    /// element would block for Playwright's 30 s timeout and then fail the task.
    /// </summary>
    public static async Task<int?> GetMaxAmount(this IPage page, TroopEnums troop)
    {
        var node = page.GetTroopRow(troop).Locator("div.cta a");
        if (await node.CountAsync() == 0) return null;

        var valueStrFixed = WebUtility.HtmlDecode(await node.First.InnerTextAsync());
        return valueStrFixed.ParseInt();
    }

    public static ILocator GetTrainButton(this IPage page)
    {
        var node = page.Locator("#s1");
        return node;
    }
}