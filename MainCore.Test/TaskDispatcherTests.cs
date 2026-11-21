using MainCore.Services;
using MainCore.Shared.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MainCore.Test;

public class TaskDispatcherTests
{
    public static TheoryData<Type> TaskTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(AppMixins).Assembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(BaseTask).IsAssignableFrom(t))
                     .OrderBy(t => t.FullName))
        {
            data.Add(type);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(TaskTypes))]
    public void EveryTask_HasARegisteredHandler(Type taskType)
    {
        using var host = AppMixins.GetHostBuilder().Build();
        using var scope = host.Services.CreateScope();

        scope.ServiceProvider.GetService(TaskDispatcher.HandlerType(taskType)).ShouldNotBeNull();
    }

    [Fact]
    public void TaskTypes_AreFound()
    {
        TaskTypes().Count.ShouldBeGreaterThanOrEqualTo(14);
    }
}
