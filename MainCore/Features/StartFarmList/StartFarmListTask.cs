using MainCore.Features.Navigate;

namespace MainCore.Features.StartFarmList;

[Handler]
public sealed partial class StartFarmListTask(
    IDbContextFactory<AppDbContext> contextFactory,
    ILogger logger,
    ChromeManager chromeManager,
    FarmBurstPlanner farmBurstPlanner,
    ToFarmListPageCommand.Handler toFarmListPageCommand,
    StartActiveFarmlistCommand.Handler startActiveFarmlistCommand,
    StartAllFarmlistCommand.Handler startAllFarmlistCommand
    )
{
    public sealed class Task(AccountId accountId) : AccountTask(accountId)
    {
        protected override string TaskName => nameof(StartFarmListTask).Humanize();
    }

    private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
    {
        if (FarmBurst.ShouldSkip(Random.Shared))
        {
            logger.Information("Skipping this farm cycle");
            return ReSchedule.Error(farmBurstPlanner.NextRun(task.AccountId));
        }

        var browser = chromeManager.Get(task.AccountId);
        Result result;
        result = await toFarmListPageCommand.HandleAsync(new(browser), cancellationToken);
        if (result.IsFailed) return result;

        if (UseStartAllButton(task.AccountId))
        {
            result = await startAllFarmlistCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed) return result;
        }
        else
        {
            var farmLists = GetActiveFarms(task.AccountId);
            if (farmLists.Count == 0)
            {
                logger.Information("No farmlist is active");
                return Skip.Error;
            }

            result = await startActiveFarmlistCommand.HandleAsync(new(browser, farmLists), cancellationToken);
            if (result.IsFailed) return result;
        }

        return ReSchedule.Error(farmBurstPlanner.NextRun(task.AccountId));
    }

    private bool UseStartAllButton(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        var useStartAllButton = context.BooleanByName(accountId, AccountSettingEnums.UseStartAllButton);
        return useStartAllButton;
    }

    private List<FarmId> GetActiveFarms(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        var farmLists = context.FarmLists
            .Where(x => x.AccountId == accountId.Value)
            .Where(x => x.IsActive)
            .Select(x => new FarmId(x.Id))
            .ToList();
        return farmLists;
    }
}