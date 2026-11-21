using Avalonia;

using MainCore;

namespace AvaloniaUI
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Start in the data folder (the app folder, or ~/.local/share/TravianBotSharp when installed
            // from the .deb), so anything opened by a relative path lands next to TBS.db.
            Directory.SetCurrentDirectory(AppPaths.DataDirectory);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Also used by the Avalonia previewer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
