using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

using MainCore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace AvaloniaUI.Test;

/// <summary>
/// Opens every view with its real view model in a headless Avalonia window and lets WhenActivated run,
/// so a binding that throws on activation (e.g. ReactiveUI's AutoDataTemplateBindingHook against a
/// ComboBox that already has DisplayMemberBinding) fails here instead of crashing the app.
/// </summary>
public class ViewActivationTests
{
    // One headless UI thread for the whole run; disposing a session blocks on its dispatcher.
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(AvaloniaUI.App), AvaloniaTestIsolationLevel.PerTest));

    public static IEnumerable<TheoryDataRow<string>> Views() =>
        FindViews().Select(v => new TheoryDataRow<string>(v.View.Name) { TestDisplayName = v.View.Name });

    private static List<(Type View, Type ViewModel)> FindViews() =>
        typeof(AvaloniaUI.App).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && t.GetConstructor(Type.EmptyTypes) is not null
                        && !(t.BaseType is { IsGenericType: true } && t.BaseType.GetGenericTypeDefinition() == typeof(ReactiveUserControl<>)))
            .Select(t => (View: t, ViewModel: ViewModelOf(t)))
            .Where(x => x.ViewModel is not null)
            .Select(x => (x.View, x.ViewModel!))
            .ToList();

    private static Type? ViewModelOf(Type t)
    {
        for (var b = t.BaseType; b is not null; b = b.BaseType)
        {
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(ReactiveUserControl<>))
                return b.GetGenericArguments()[0];
        }
        return null;
    }

    [Fact]
    public void Every_view_is_discovered()
    {
        FindViews().Count.ShouldBeGreaterThan(10);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public async Task View_activates_without_throwing(string viewName)
    {
        var (viewType, vmType) = FindViews().Single(v => v.View.Name == viewName);

        await Session.Value.Dispatch(() =>
        {
            using var host = BuildHost();
            var vm = ActivatorUtilities.GetServiceOrCreateInstance(host.Services, vmType);

            var errors = new List<Exception>();
            void OnUnhandled(object? s, DispatcherUnhandledExceptionEventArgs e)
            {
                errors.Add(e.Exception);
                e.Handled = true;
            }
            Dispatcher.UIThread.UnhandledException += OnUnhandled;
            try
            {
                var view = (Control)Activator.CreateInstance(viewType)!;
                ((IViewFor)view).ViewModel = vm;
                var window = new Window { Content = view, Width = 1200, Height = 800 };
                window.Show();
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
                window.Close();
            }
            finally
            {
                Dispatcher.UIThread.UnhandledException -= OnUnhandled;
            }
            errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        }, CancellationToken.None);
    }

    private static IHost BuildHost() =>
        AppMixins.GetHostBuilder()
            .ConfigureServices(services =>
            {
                services.UseMicrosoftDependencyResolver();
                var builder = AppLocator.CurrentMutable.CreateReactiveUIBuilder();
                builder.WithRegistration(r => r.InitializeSplat());
                builder.WithAvalonia();
                builder.BuildApp();
            })
            .Build();
}
