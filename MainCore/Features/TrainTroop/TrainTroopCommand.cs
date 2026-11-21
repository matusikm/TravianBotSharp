namespace MainCore.Features.TrainTroop;

[Handler]
public sealed partial class TrainTroopCommand(ILogger logger)
{
    public sealed record Command(IChromeBrowser Browser, TroopEnums Troop, int Amount, bool TrainWhenLowResource) : ICommand
    {
        public string Name => $"{nameof(TrainTroopCommand).Humanize()} [{Description}]";
        private string Description => $"Train {Amount} {Troop}";
    }

    private async ValueTask<Result> HandleAsync(Command command)
    {
        var (browser, troop, amount, trainWhenLowResource) = command;

        var maxAmount = await browser.CurrentPage.GetMaxAmount(troop);
        var amountResult = AmountToTrain(troop, amount, maxAmount, trainWhenLowResource);
        if (amountResult.IsFailed)
        {
            if (amountResult.HasError<TroopNotAvailable>())
            {
                logger.Warning("Cannot train {Troop} because this building doesn't offer it yet. Research it first or choose another troop", troop);
            }
            else
            {
                logger.Warning("Cannot train {Amount} {Troop} because of missing resources", amount, troop);
            }
            return amountResult.ToResult();
        }

        if (amountResult.Value < amount)
        {
            logger.Information("Resources are enough for {MaxAmount} {Troop}, so training those instead of {Amount}", amountResult.Value, troop, amount);
            amount = amountResult.Value;
        }

        Result result;
        result = await browser.Input(browser.CurrentPage.GetInputBox(troop), $"{amount}");
        if (result.IsFailed) return result;

        result = await browser.Click(browser.CurrentPage.GetTrainButton());
        if (result.IsFailed) return result;

        logger.Information("Train {Amount} {Troop} is done.", amount, troop);
        return Result.Ok();
    }

    /// <summary>
    /// The amount to type into the training box. With "train when low resource" on, a short village trains as many
    /// as it can afford instead of asking the game for more than that.
    /// </summary>
    public static Result<int> AmountToTrain(TroopEnums troop, int amount, int? maxAmount, bool trainWhenLowResource)
    {
        if (maxAmount is null) return TroopNotAvailable.Error(troop);
        if (maxAmount <= 0) return MissingResource.Error;
        if (amount <= maxAmount) return amount;
        if (!trainWhenLowResource) return MissingResource.Error;
        return maxAmount.Value;
    }
}