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
            UseBundledPlaywrightDriver();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>
        /// In the macOS app bundle the Patchright driver lives in Contents/Resources/.playwright,
        /// because code signing does not allow folders next to the executable in Contents/MacOS.
        /// </summary>
        private static void UseBundledPlaywrightDriver()
        {
            if (!OperatingSystem.IsMacOS()) return;
            if (Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH") is not null) return;

            var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources"));
            if (Directory.Exists(Path.Combine(resources, ".playwright")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", resources);
            }
        }

        // Also used by the Avalonia previewer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
