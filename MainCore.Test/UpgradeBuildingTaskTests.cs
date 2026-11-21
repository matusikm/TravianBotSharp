using FluentResults;
using MainCore.Features.UpgradeBuilding;
using MainCore.Shared.Errors;

namespace MainCore.Test;

public class UpgradeBuildingTaskTests
{
    [Fact]
    public void PrerequisiteInQueue_ReschedulesAtEarliestCompletion()
    {
        var soon = new DateTime(2026, 11, 21, 10, 0, 0);
        var later = soon.AddMinutes(30);
        IError[] errors =
        [
            new UpgradeErrorNextExecute(UpgradeErrorType.PrerequisiteInQueue, later),
            new UpgradeErrorNextExecute(UpgradeErrorType.PrerequisiteInQueue, soon),
        ];

        var result = UpgradeBuildingTask.RescheduleOrStop(errors);

        result.HasError<ReSchedule>().ShouldBeTrue();
        result.Errors.OfType<ReSchedule>().Single().ExecuteAt.ShouldBe(soon);
    }

    [Fact]
    public void PrerequisiteMissing_Stops()
    {
        IError[] errors = [new UpgradeError(UpgradeErrorType.PrerequisiteMissing)];

        var result = UpgradeBuildingTask.RescheduleOrStop(errors);

        result.HasError<Stop>().ShouldBeTrue();
    }
}
