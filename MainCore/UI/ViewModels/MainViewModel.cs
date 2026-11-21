using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

using MainCore.UI.ViewModels.Abstract;
using MainCore.UI.ViewModels.UserControls;

using Microsoft.Win32;

using ReactiveUI.Primitives.Signals;

using Serilog;

using Splat;

namespace MainCore.UI.ViewModels
{
    [RegisterSingleton<MainViewModel>]
    public partial class MainViewModel(
        WaitingOverlayViewModel waitingOverlayViewModel,
        IDbContextFactory<AppDbContext> contextFactory,
        ChromeManager chromeManager)
        : ViewModelBase
    {
        [Reactive]
        private MainLayoutViewModel _mainLayoutViewModel = null!;

        public async Task Load()
        {
            waitingOverlayViewModel.Show();

            await Signal.Start(() =>
            {
                CheckDriver();

                using var context = contextFactory.CreateDbContext();
                DatabaseInitializer.Initialize(context);
            }, RxSchedulers.TaskpoolScheduler);

            waitingOverlayViewModel.ChangeMessage("loading program layout");
            MainLayoutViewModel = Locator.Current.GetService<MainLayoutViewModel>()!;
            await MainLayoutViewModel.Load();

            waitingOverlayViewModel.Hide();
        }

        public async Task Unload()
        {
            await chromeManager.Shutdown();
            await Log.CloseAndFlushAsync();
        }

        private static void CheckDriver()
        {
            var chromeVersion = OperatingSystem.IsWindows()
                ? GetWindowsChromeVersion()
                : GetUnixChromeVersion();

            if (chromeVersion.Major <= 114)
            {
                throw new Exception($"Your chrome version is {chromeVersion}. Please update your chrome first");
            }
        }

        [SupportedOSPlatform("windows")]
        private static Version GetWindowsChromeVersion()
        {
            var chromeVersionString = GetRegistryValue("HKEY_CURRENT_USER\\SOFTWARE\\Google\\Chrome\\BLBeacon", "version");
            if (!string.IsNullOrEmpty(chromeVersionString))
            {
                return new Version(chromeVersionString);
            }

            chromeVersionString = GetRegistryValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Google Chrome", "version");
            if (!string.IsNullOrEmpty(chromeVersionString))
            {
                return new Version(chromeVersionString);
            }

            var chromePath = GetRegistryValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe");
            if (string.IsNullOrEmpty(chromePath))
            {
                throw new Exception("Chrome is not installed");
            }

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(chromePath);
            if (string.IsNullOrEmpty(fileVersionInfo.FileVersion))
            {
                throw new Exception($"Chrome is installed but failed to get chrome version [{fileVersionInfo.FileVersion}]");
            }
            return new Version(fileVersionInfo.FileVersion);
        }

        [SupportedOSPlatform("windows")]
        private static string? GetRegistryValue(string registryPath, string? valueName = null)
        {
            try
            {
                return Registry.GetValue(registryPath, valueName, null) as string;
            }
            catch
            {
                return null;
            }
        }

        // Playwright's "chrome" channel uses these fixed install locations on Linux and macOS.
        private static readonly string[] UnixChromePaths =
        [
            "/opt/google/chrome/chrome",
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        ];

        private static Version GetUnixChromeVersion()
        {
            var chromePath = UnixChromePaths.FirstOrDefault(File.Exists)
                ?? throw new Exception("Google Chrome is not installed. On Ubuntu install the google-chrome-stable package from https://www.google.com/chrome/");

            string output;
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = chromePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }) ?? throw new Exception("Failed to start chrome");
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10_000);
            }
            catch (Exception ex)
            {
                throw new Exception($"Chrome is installed but failed to get chrome version: {ex.Message}", ex);
            }

            // e.g. "Google Chrome 142.0.7444.59"
            var match = ChromeVersionRegex().Match(output);
            if (!match.Success)
            {
                throw new Exception($"Chrome is installed but failed to get chrome version [{output.Trim()}]");
            }
            return new Version(match.Value);
        }

        [GeneratedRegex(@"\d+(\.\d+){1,3}")]
        private static partial Regex ChromeVersionRegex();
    }
}