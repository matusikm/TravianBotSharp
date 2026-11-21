using System.Diagnostics;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using AvaloniaUI.Services;
using AvaloniaUI.Views;

using MainCore;
using MainCore.Services;
using MainCore.UI.ViewModels;
using MainCore.UI.ViewModels.UserControls;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

using Serilog;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace AvaloniaUI
{
    public partial class App : Application
    {
        private IHost? _host;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Same wiring as WPFUI (CP.Extensions.Hosting.ReactiveUI.Wpf), with the Avalonia
                // scheduler and view hooks instead of the WPF dispatcher.
                _host = AppMixins.GetHostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.UseMicrosoftDependencyResolver();
                        var builder = AppLocator.CurrentMutable.CreateReactiveUIBuilder();
                        builder.WithRegistration(r => r.InitializeSplat());
                        builder.WithExceptionHandler(new LoggingExceptionObserver());
                        builder.WithAvalonia();
                        builder.BuildApp();
                    })
                    .Build();

                _host.Services.UseMicrosoftDependencyResolver();

                var services = _host.Services;
                DialogHandlers.Register(services.GetRequiredService<DialogService>(), () => desktop.MainWindow);
                services.GetRequiredService<RxQueue>().Setup();

                _host.Start();

                desktop.MainWindow = new MainWindow(
                    services.GetRequiredService<MainViewModel>(),
                    services.GetRequiredService<WaitingOverlayViewModel>());

                // Ctrl+C / SIGTERM stop the host; close the window so the bot shuts Chrome down and the app exits.
                services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() =>
                    Dispatcher.UIThread.Post(() => desktop.MainWindow?.Close()));

                desktop.Exit += (_, _) =>
                {
                    _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    _host.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private sealed class LoggingExceptionObserver : IObserver<Exception>
        {
            public void OnNext(Exception value)
            {
                Trace.TraceError($"[ReactiveUI] Unhandled exception: {value}");
                Log.ForContext<LoggingExceptionObserver>()
                    .Error(value, "Unhandled exception");
                DialogHandlers.ShowError("Error", "There is something wrong. Please check logs/logs-Other.txt.");

                if (!Debugger.IsAttached) return;
                Debugger.Break();
            }

            public void OnError(Exception error) => OnNext(error);

            public void OnCompleted()
            {
            }
        }
    }
}
