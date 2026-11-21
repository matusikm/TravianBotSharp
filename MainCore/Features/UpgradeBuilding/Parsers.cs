namespace MainCore.Features.UpgradeBuilding;

public static class Parsers
{
    public static async Task<bool> IsBuildingExist(this IPage page, BuildingEnums buildingType, int location)
    {
        if (buildingType.IsResourceField())
        {
            var field = page.GetFieldLocator(buildingType, location);
            return await field.CountAsync() > 0;
        }
        else
        {
            var building = page.GetBuildingLocator(buildingType, location);
            return await building.CountAsync() > 0;
        }
    }

    public static async Task<int> GetLevel(this IPage page, BuildingEnums buildingType, int location)
    {
        if (buildingType.IsResourceField())
        {
            var field = page.GetFieldLocator(buildingType, location).Locator("div.labelLayer");
            var levelStr = await field.InnerTextAsync();
            var levelInt = int.TryParse(levelStr, out var parsedLevel) ? parsedLevel : 0;
            return levelInt;
        }
        else
        {
            var building = page.GetBuildingLocator(buildingType, location).Locator("a.level");
            var levelStr = await building.GetAttributeAsync("data-level");
            var levelInt = int.TryParse(levelStr, out var parsedLevel) ? parsedLevel : 0;
            return levelInt;
        }
    }

    public static async Task<bool> IsUnderConstruction(this IPage page, BuildingEnums buildingType, int location)
    {
        if (buildingType.IsResourceField())
        {
            var field = page.GetFieldLocator(buildingType, location);
            var classes = await field.GetAttributeAsync("class");
            if (string.IsNullOrEmpty(classes)) return false;
            return classes.Contains("underConstruction");
        }
        else
        {
            var building = page.GetBuildingLocator(buildingType, location).Locator("a.level");
            var classes = await building.GetAttributeAsync("class");
            if (string.IsNullOrEmpty(classes)) return false;
            return classes.Contains("underConstruction");
        }
    }

    public static async Task<ILocator> GetPanel(this IPage page, BuildingEnums building)
    {
        if (await IsEmptySite(page))
        {
            return page.Locator($"#contract_building{(int)building} div.upgradeBuilding");
        }
        else
        {
            return page.Locator("div.upgradeBuilding");
        }
    }

    public static async Task<ILocator> GetRequiredResource(this IPage page, BuildingEnums building)
    {
        var panel = await page.GetPanel(building);
        var nodes = panel.Locator("#contract div.resourceWrapper div.resource");
        return nodes;
    }

    public static async Task<TimeSpan> GetTimeWhenEnoughResource(this IPage page, BuildingEnums building)
    {
        var panel = await page.GetPanel(building);
        var node = panel.Locator("#contract div.errorMessage span.timer");
        var timeValue = await node.GetAttributeAsync("value");
        return TimeSpan.FromSeconds(int.Parse(timeValue ?? "0"));
    }

    public static async Task<ILocator> GetNormalButton(this IPage page, BuildingEnums building)
    {
        if (await IsEmptySite(page))
        {
            return page.Locator($"#contract_building{(int)building} div.upgradeButtonsContainer .section1 button.new");
        }
        else
        {
            return page.Locator("div.upgradeButtonsContainer .section1 button.build");
        }
    }

    public static async Task<ILocator> GetSpecialButton(this IPage page, BuildingEnums building)
    {
        if (await IsEmptySite(page))
        {
            return page.Locator($"#contract_building{(int)building} div.upgradeButtonsContainer .section2 button.videoFeatureButton");
        }
        else
        {
            return page.Locator("div.upgradeButtonsContainer .section2 button.videoFeatureButton");
        }
    }

    /// <summary>
    /// Build time of the normal upgrade, as shown next to the left upgrade button, or null when it can't be read.
    /// </summary>
    public static async Task<TimeSpan?> GetUpgradeDuration(this IPage page, BuildingEnums building)
    {
        var node = await IsEmptySite(page)
            ? page.Locator($"#contract_building{(int)building} div.upgradeButtonsContainer .section1 .duration .value")
            : page.Locator("div.upgradeButtonsContainer .section1 .duration .value");
        if (await node.CountAsync() == 0) return null;
        return ParseDuration(await node.First.InnerTextAsync());
    }

    /// <summary>
    /// Parses Travian's "h:mm:ss" timer text. Hours can go past 24 and the text can carry
    /// direction marks, so it is split by hand instead of using TimeSpan.Parse.
    /// </summary>
    public static TimeSpan? ParseDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var digits = new string(text.Where(c => char.IsAsciiDigit(c) || c == ':').ToArray());
        var parts = digits.Split(':');
        if (parts.Length is < 2 or > 3 || parts.Any(x => x.Length == 0)) return null;

        var numbers = parts.Select(int.Parse).ToArray();
        return numbers.Length == 3
            ? new TimeSpan(numbers[0], numbers[1], numbers[2])
            : new TimeSpan(0, numbers[0], numbers[1]);
    }

    private static async Task<bool> IsEmptySite(this IPage page)
    {
        var parentPanel = page.Locator("#build");
        var classes = await parentPanel.GetAttributeAsync("class");
        return classes is not null && classes.Contains("gid0");
    }
}