using Avalonia.Headless;

using MainCore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace AvaloniaUI.Test;

/// <summary>Shared by every test class: Avalonia can only be initialised once per process.</summary>
internal static class TestApp
{
    // One headless UI thread for the whole run; disposing a session blocks on its dispatcher.
    public static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(AvaloniaUI.App), AvaloniaTestIsolationLevel.PerTest));

    public static IHost BuildHost() =>
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
