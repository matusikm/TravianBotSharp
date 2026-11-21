using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace MainCore.Services;

/// <summary>
/// Runs a task through its generated handler (<c>IHandler&lt;TTask, Result&gt;</c>).
/// Replaces a hand-written switch that had to be edited for every new task type;
/// a test checks that every task type has a handler registered.
/// </summary>
public static class TaskDispatcher
{
    private delegate ValueTask<Result> Dispatch(IServiceProvider services, BaseTask task, CancellationToken cancellationToken);

    private static readonly ConcurrentDictionary<Type, Dispatch> _dispatchers = new();

#pragma warning disable S3011 // Reflection on our own private generic method; no accessibility bypass of other code.
    private static readonly MethodInfo _invoke = typeof(TaskDispatcher)
        .GetMethod(nameof(Invoke), BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning restore S3011

    public static ValueTask<Result> Execute(IServiceProvider services, BaseTask task, CancellationToken cancellationToken) =>
        _dispatchers.GetOrAdd(task.GetType(), Create)(services, task, cancellationToken);

    /// <summary>The handler type a task of <paramref name="taskType"/> is sent to.</summary>
    public static Type HandlerType(Type taskType) => typeof(IHandler<,>).MakeGenericType(taskType, typeof(Result));

    private static Dispatch Create(Type taskType) =>
        _invoke.MakeGenericMethod(taskType).CreateDelegate<Dispatch>();

    private static ValueTask<Result> Invoke<TTask>(IServiceProvider services, BaseTask task, CancellationToken cancellationToken)
        where TTask : BaseTask
    {
        var handler = services.GetRequiredService<IHandler<TTask, Result>>();
        return handler.HandleAsync((TTask)task, cancellationToken);
    }
}
