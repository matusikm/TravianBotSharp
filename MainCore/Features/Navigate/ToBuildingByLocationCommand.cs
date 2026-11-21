using System.Web;

namespace MainCore.Features.Navigate;

[Handler]
public static partial class ToBuildingByLocationCommand
{
    public sealed record Command(IChromeBrowser Browser, int Location) : ICommand
    {
        public string Name => $"{nameof(ToBuildingByLocationCommand)} [{Description}]";
        private string Description => $"{Location}";
    }

    private const float BuildingClickTimeout = 5_000;

    private static async ValueTask<Result> HandleAsync(Command command)
    {
        var (browser, location) = command;
        Result result;
        if (location < 19)
        {
            var field = browser.CurrentPage.GetFieldButton(location);
            result = await browser.Click(field);
            if (result.IsFailed) return result;
        }
        else
        {
            var building = browser.CurrentPage.GetBuildingButton(location);

            // Click the building like a player would. Its shape is an SVG path, so a point inside the
            // bounding box can miss it; then fall back to running the building's own onclick script.
            result = await browser.Click(building, BuildingClickTimeout);
            if (result.IsFailed)
            {
                var javascript = await building.GetAttributeAsync("onclick");
                var decodedJs = HttpUtility.HtmlDecode(javascript ?? "");

                result = await browser.ExecuteJsScript(decodedJs);
                if (result.IsFailed) return result;
            }
        }

        result = await browser.Wait(browser.CurrentPage.GetBuildPanel());
        if (result.IsFailed) return result;
        return Result.Ok();
    }
}