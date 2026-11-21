using MainCore.Features.UseHeroItem;

namespace MainCore.Test;

public class HeroResourceParserTests(PlaywrightFixture fixture) : IClassFixture<PlaywrightFixture>
{
    [Theory]
    [InlineData("1234", 1234)]
    [InlineData("1,234", 1234)]
    [InlineData("1.234", 1234)]
    [InlineData("1 234", 1234)]
    [InlineData("1 234 567", 1234567)]
    [InlineData("‭12,345‬", 12345)]
    [InlineData("12.3k", 12300)]
    [InlineData("1,5m", 1500000)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("abc", 0)]
    public void ParseCount_HandlesSeparatorsAndAbbreviations(string? text, long expected)
    {
        MainCore.Features.UseHeroItem.Parsers.ParseCount(text).ShouldBe(expected);
    }

    [Fact]
    public async Task GetInventoryResources_ReadsEachRowByItsInput()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        // Rows in a different order than wood, clay, iron, crop, and an unrelated item before them.
        await page.SetContentAsync("""
            <div class="heroItem"><div class="item item112"></div><div class="count">7</div></div>
            <div class="resourceTransferDialog">
              <div class="resourceRow"><div class="heroItem"><div class="count">4,000</div></div><input name="crop"></div>
              <div class="resourceRow"><div class="heroItem"><div class="count">1,234</div></div><input name="lumber"></div>
              <div class="resourceRow"><div class="heroItem"><div class="count">2 000</div></div><input name="clay"></div>
              <div class="resourceRow"><div class="heroItem"><div class="count">3.000</div></div><input name="iron"></div>
            </div>
            """);

        var resources = await page.GetInventoryResources();

        resources.ShouldBe([1234L, 2000L, 3000L, 4000L]);
    }

    [Fact]
    public async Task GetInventoryResources_FallsBackToPageOrder_WithoutTransferRows()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("""
            <div class="heroItem"><div class="count">1,500</div></div>
            <div class="heroItem"><div class="count">20</div></div>
            <div class="heroItem"></div>
            <div class="heroItem"><div class="count">12.3k</div></div>
            """);

        var resources = await page.GetInventoryResources();

        resources.ShouldBe([1500L, 20L, 0L, 12300L]);
    }
}
