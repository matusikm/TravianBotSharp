using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

using AvaloniaUI.Views.Tabs.Villages;

using MainCore;
using MainCore.UI.ViewModels.Tabs.Villages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using ReactiveUI.Primitives;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

using JobItem = MainCore.UI.Models.Output.ListBoxItem;

namespace AvaloniaUI.Test;

/// <summary>Drags rows of the Build tab's job list with a headless mouse.</summary>
public class BuildTabDragTests
{

    [Fact]
    public async Task Dragging_last_job_onto_first_marks_drop_and_runs_move()
    {
        await TestApp.Session.Value.Dispatch(() =>
        {
            using var host = TestApp.BuildHost();
            var (window, view, vm) = Open(host);
            var executions = 0;
            using var _ = vm.MoveToCommand.IsExecuting.Subscribe(x => { if (x) executions++; });

            var grid = view.FindControl<ListBox>("JobsGrid")!;
            var last = Center(window, grid, 3);
            var first = Center(window, grid, 0);

            window.MouseDown(last, MouseButton.Left);
            window.MouseMove(new Point(last.X, last.Y - 10), RawInputModifiers.LeftMouseButton);
            window.MouseMove(first, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            ((ListBoxItem)grid.ContainerFromIndex(0)!).Classes.ShouldContain("dropAbove");

            window.MouseUp(first, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            ((ListBoxItem)grid.ContainerFromIndex(0)!).Classes.ShouldNotContain("dropAbove");
            executions.ShouldBe(1);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Plain_click_selects_without_moving()
    {
        await TestApp.Session.Value.Dispatch(() =>
        {
            using var host = TestApp.BuildHost();
            var (window, view, vm) = Open(host);
            var executions = 0;
            using var _ = vm.MoveToCommand.IsExecuting.Subscribe(x => { if (x) executions++; });

            var grid = view.FindControl<ListBox>("JobsGrid")!;
            var row = Center(window, grid, 2);
            window.MouseDown(row, MouseButton.Left);
            window.MouseUp(row, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            executions.ShouldBe(0);
            grid.SelectedIndex.ShouldBe(2);
            window.Close();
        }, CancellationToken.None);
    }

    private static (Window, BuildTab, BuildViewModel) Open(IHost host)
    {
        var vm = ActivatorUtilities.GetServiceOrCreateInstance<BuildViewModel>(host.Services);
        var view = new BuildTab { ViewModel = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        vm.Jobs.Load([.. Enumerable.Range(1, 4).Select(i => new JobItem { Id = i, Content = $"Job {i}" })]);
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        return (window, view, vm);
    }

    private static Point Center(Window window, ListBox grid, int index)
    {
        var item = (Control)grid.ContainerFromIndex(index)!;
        return item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
    }

}
