using MainCore.Features.UpgradeBuilding;

namespace MainCore.Features.UseHeroItem;

public static class Parsers
{
    public static ILocator GetInventoryPage(this IPage page)
    {
        var inventory = page.Locator("#heroV2 .inventoryPageWrapper .inventoryWrapper");
        return inventory;
    }

    public static ILocator GetInventoryPageWrapper(this IPage page)
    {
        var inventory = page.Locator("#heroV2 .inventoryPageWrapper");
        return inventory;
    }

    public static async Task<bool> IsInventoryPageLoaded(this IPage page)
    {
        var inventoryPageWrapper = page.Locator("#heroV2 .inventoryPageWrapper");
        var count = await inventoryPageWrapper.CountAsync();
        var loading = await inventoryPageWrapper.EvaluateAsync<bool>("node => node.classList.contains('loading')");
        return count > 0 && !loading;
    }

    /// <summary>
    /// Wood, clay, iron and crop in the hero inventory, read from the open resource transfer dialog. Each count is
    /// taken from the row of its own input (lumber, clay, iron, crop), so other items or a different order can't
    /// swap them. Falls back to the page order of hero items when the dialog has no such rows.
    /// </summary>
    public static async Task<long[]> GetInventoryResources(this IPage page)
    {
        var texts = await page.EvaluateJsonAsync<string?[]>(@"() => {
                const names = ['lumber', 'clay', 'iron', 'crop'];
                const countText = (root) => {
                    const count = root ? root.querySelector('.heroItem .count') : null;
                    return count ? (count.innerText || count.textContent) : null;
                };
                const rows = names.map(name => {
                    const input = document.querySelector(`div.resourceTransferDialog .resourceRow input[name='${name}']`);
                    return input ? input.closest('.resourceRow') : null;
                });
                if (rows.every(row => row !== null)) return rows.map(countText);

                const cells = Array.from(document.querySelectorAll('div.heroItem')).slice(0, 4);
                return names.map((_, i) => {
                    const count = cells[i] ? cells[i].querySelector('.count') : null;
                    return count ? (count.innerText || count.textContent) : null;
                });
            }");
        return [.. texts.Select(ParseCount)];
    }

    /// <summary>
    /// Parses an item count such as "1234", "1,234", "1.234", "1 234" or "12.3k". Thousand separators depend on
    /// the server language, and parseInt stopped at the first one ("1,234" became 1). Unreadable text is 0, so
    /// the bot reports a shortage instead of spending resources it may not have.
    /// </summary>
    public static long ParseCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var clean = new string([.. text.Where(c => !char.IsWhiteSpace(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format)]).ToLowerInvariant();

        var multiplier = 1L;
        if (clean.EndsWith('k')) multiplier = 1_000;
        else if (clean.EndsWith('m')) multiplier = 1_000_000;
        if (multiplier != 1)
        {
            // Rounded display: read the number with its decimal part and round down, never up.
            var number = clean[..^1].Replace(',', '.');
            return decimal.TryParse(number, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? (long)Math.Floor(value * multiplier)
                : 0;
        }

        if (!clean.All(c => char.IsAsciiDigit(c) || c is ',' or '.' or '\'' or '’')) return 0;
        var digits = new string([.. clean.Where(char.IsAsciiDigit)]);
        return long.TryParse(digits, out var count) ? count : 0;
    }

    public static ILocator GetItemSlot(this IPage page, HeroItemEnums type)
    {
        var item = page.Locator($"div.heroItems div.heroItem:not(.empty):has(.item{(int)type})");
        return item;
    }

    public static ILocator GetAmountBox(this IPage page, string name)
    {
        var box = page.Locator($"div.resourceTransferDialog .resourceRow input[name='{name}']");
        return box;
    }

    public static ILocator GetConfirmButton(this IPage page)
    {
        var button = page.Locator("div.resourceTransferDialog .actionButton button:nth-of-type(2)");
        return button;
    }

    public static ILocator GetResourceConfirmButton(this IPage page)
    {
        var button = page.Locator("div.resourceTransferDialog .actionButton button");
        return button;
    }

    public static ILocator GetResourceTransferDialog(this IPage page)
    {
        var dialog = page.Locator("div.resourceTransferDialog");
        return dialog;
    }

    public static ILocator GetSuccessToast(this IPage page)
    {
        var toast = page.Locator("div.toast.toastSuccess");
        return toast;
    }

    public static async Task<ILocator> GetFillUpButton(this IPage page, BuildingEnums building)
    {
        var panel = await page.GetPanel(building);
        var node = panel.Locator("#contract .resourceWrapper .resource.transfer.fillUp");
        return node.First;
    }
}