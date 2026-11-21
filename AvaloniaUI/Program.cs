using Avalonia;

namespace AvaloniaUI
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // MainCore keeps TBS.db, logs/ and Data/ relative to the working directory,
            // like the Windows build does when started from its own folder.
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

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
