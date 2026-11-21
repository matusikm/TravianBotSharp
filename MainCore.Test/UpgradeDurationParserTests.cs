using MainCore.Features.UpgradeBuilding;
using MainCore.Shared.Enums;

namespace MainCore.Test;

public class UpgradeDurationParserTests(PlaywrightFixture fixture) : IClassFixture<PlaywrightFixture>
{
    [Theory]
    [InlineData("0:02:20", 0, 2, 20)]
    [InlineData("1:05:00", 1, 5, 0)]
    [InlineData("26:10:05", 26, 10, 5)]
    [InlineData("‭0:25:50‬", 0, 25, 50)]
    [InlineData(" 12:30 ", 0, 12, 30)]
    public void ParseDuration_ReadsTravianTimers(string text, int hours, int minutes, int seconds)
    {
        MainCore.Features.UpgradeBuilding.Parsers.ParseDuration(text).ShouldBe(new TimeSpan(hours, minutes, seconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("1:")]
    [InlineData("1:2:3:4")]
    public void ParseDuration_ReturnsNull_ForUnreadableText(string? text)
    {
        MainCore.Features.UpgradeBuilding.Parsers.ParseDuration(text).ShouldBeNull();
    }

    // Trimmed from a real cropland upgrade page: the left button's duration, not the video one.
    [Fact]
    public async Task GetUpgradeDuration_ReadsNormalButtonTime_OnBuiltSite()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("""
            <div id="build" class="gid4">
              <div class="upgradeBuilding">
                <div id="contract" class="contractWrapper"></div>
                <div class="upgradeButtonsContainer section2Enabled">
                  <div class="section1">
                    <button class="textButtonV1 green build">Upgrade to level 2</button>
                    <div class="inlineIcon duration" title="duration"><i class="clock_medium"></i><span class="value ">0:42:20</span></div>
                  </div>
                  <div class="section2">
                    <button class="textButtonV1 green build videoFeatureButton" time="1905">Upgrade 25% faster</button>
                    <div class="inlineIcon duration" title="duration"><i class="clock_medium"></i><span class="value ">0:31:45</span></div>
                  </div>
                </div>
              </div>
            </div>
            """);

        var duration = await page.GetUpgradeDuration(BuildingEnums.Cropland);

        duration.ShouldBe(new TimeSpan(0, 42, 20));
    }

    // Trimmed from a real empty building site: each building has its own contract block.
    [Fact]
    public async Task GetUpgradeDuration_ReadsTheChosenBuilding_OnEmptySite()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("""
            <div id="build" class="gid0">
              <div id="contract_building23" class="contract contractNew contractWrapper">
                <div class="upgradeButtonsContainer"><div class="section1">
                  <button class="textButtonV1 green new">Build</button>
                  <div class="inlineIcon duration"><span class="value ">0:01:30</span></div>
                </div></div>
              </div>
              <div id="contract_building25" class="contract contractNew contractWrapper">
                <div class="upgradeButtonsContainer"><div class="section1">
                  <button class="textButtonV1 green new">Build</button>
                  <div class="inlineIcon duration"><span class="value ">0:10:20</span></div>
                </div></div>
              </div>
            </div>
            """);

        var duration = await page.GetUpgradeDuration((BuildingEnums)25);

        duration.ShouldBe(new TimeSpan(0, 10, 20));
    }

    [Fact]
    public async Task GetUpgradeDuration_ReturnsNull_WhenNoTimerShown()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("""<div id="build" class="gid4"><div class="upgradeButtonsContainer"><div class="section1"></div></div></div>""");

        (await page.GetUpgradeDuration(BuildingEnums.Cropland)).ShouldBeNull();
    }
}
