using MainCore.Features.Sleep;
using MainCore.Infrasturecture.Entities;
using MainCore.Services;

namespace MainCore.Test;

public class OverdueTasksTests
{
    private static readonly AccountId Account = new(1);
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0);

    [Fact]
    public void Spread_KeepsFirstOverdueTask_AndMovesTheRestApartInOrder()
    {
        var tasks = Enumerable.Range(0, 6)
            .Select(i => new SleepTask.Task(Account) { ExecuteAt = Now.AddMinutes(-30 + i) })
            .ToList();

        var moved = OverdueTasks.Spread(tasks, Now, new Random(7));

        moved.ShouldBe(5);
        tasks[0].ExecuteAt.ShouldBe(Now.AddMinutes(-30));
        for (var i = 1; i < tasks.Count; i++)
        {
            var previous = i == 1 ? Now : tasks[i - 1].ExecuteAt;
            var gap = tasks[i].ExecuteAt - previous;
            gap.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(OverdueTasks.MinGapSeconds));
        }
    }

    [Fact]
    public void Spread_LeavesTasksThatAreNotDue()
    {
        var later = new SleepTask.Task(Account) { ExecuteAt = Now.AddHours(1) };
        var overdue = new SleepTask.Task(Account) { ExecuteAt = Now.AddMinutes(-5) };

        var moved = OverdueTasks.Spread([later, overdue], Now, new Random(1));

        moved.ShouldBe(0);
        later.ExecuteAt.ShouldBe(Now.AddHours(1));
        overdue.ExecuteAt.ShouldBe(Now.AddMinutes(-5));
    }
}
