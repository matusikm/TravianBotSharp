using System.Diagnostics;
using MainCore.Features.TrainTroop;
using MainCore.Shared.Enums;
using MainCore.Shared.Errors;

namespace MainCore.Test;

public class TrainTroopTests(PlaywrightFixture fixture) : IClassFixture<PlaywrightFixture>
{
    private const string BarracksPage = """
        <div id="build" class="gid19">
          <div class="troop">
            <div class="details"><img class="unit u1" alt="Legionnaire"></div>
            <div class="cta"><input type="text" class="text" name="t1" value="0"><a href="#">&nbsp;/&nbsp;1,234</a></div>
          </div>
          <div class="troop empty">
            <div class="details"><img class="unit u2" alt="Praetorian"></div>
          </div>
          <button id="s1">Train</button>
        </div>
        """;

    [Fact]
    public async Task GetMaxAmount_ReadsTheTroopsLink()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(BarracksPage);

        (await page.GetMaxAmount(TroopEnums.Legionnaire)).ShouldBe(1234);
    }

    [Theory]
    [InlineData(TroopEnums.Praetorian)] // row is there but empty
    [InlineData(TroopEnums.Imperian)] // no row: not researched
    public async Task GetMaxAmount_ReturnsNullAtOnce_WhenTheTroopCantBeTrained(TroopEnums troop)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(BarracksPage);

        var stopwatch = Stopwatch.StartNew();
        var amount = await page.GetMaxAmount(troop);

        amount.ShouldBeNull();
        // Before the fix this waited for Playwright's 30 s timeout and threw.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AmountToTrain_NotResearched_IsTroopNotAvailable()
    {
        var result = TrainTroopCommand.AmountToTrain(TroopEnums.Imperian, 10, null, trainWhenLowResource: true);

        result.HasError<TroopNotAvailable>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmountToTrain_NothingAffordable_IsMissingResource(bool trainWhenLowResource)
    {
        var result = TrainTroopCommand.AmountToTrain(TroopEnums.Legionnaire, 10, 0, trainWhenLowResource);

        result.HasError<MissingResource>().ShouldBeTrue();
    }

    [Fact]
    public void AmountToTrain_Affordable_KeepsTheAmount()
    {
        var result = TrainTroopCommand.AmountToTrain(TroopEnums.Legionnaire, 10, 25, trainWhenLowResource: false);

        result.Value.ShouldBe(10);
    }

    [Fact]
    public void AmountToTrain_TooMany_WithoutTrainWhenLowResource_IsMissingResource()
    {
        var result = TrainTroopCommand.AmountToTrain(TroopEnums.Legionnaire, 10, 4, trainWhenLowResource: false);

        result.HasError<MissingResource>().ShouldBeTrue();
    }

    [Fact]
    public void AmountToTrain_TooMany_WithTrainWhenLowResource_TrainsWhatIsAffordable()
    {
        var result = TrainTroopCommand.AmountToTrain(TroopEnums.Legionnaire, 10, 4, trainWhenLowResource: true);

        result.Value.ShouldBe(4);
    }
}
