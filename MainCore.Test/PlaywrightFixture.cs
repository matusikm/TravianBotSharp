using Microsoft.Playwright;

namespace MainCore.Test
{
    public sealed class PlaywrightFixture : IAsyncLifetime
    {
        public IPlaywright PlaywrightInstance { get; private set; } = null!;
        public IBrowser Browser { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            PlaywrightInstance = await Playwright.CreateAsync();
            var options = new BrowserTypeLaunchOptions { Headless = true };
            var executable = Environment.GetEnvironmentVariable("TBS_TEST_CHROMIUM");
            if (string.IsNullOrEmpty(executable)) options.Channel = "chrome";
            else options.ExecutablePath = executable;
            Browser = await PlaywrightInstance.Chromium.LaunchAsync(options);
        }

        public async ValueTask DisposeAsync()
        {
            await Browser.DisposeAsync();
            PlaywrightInstance.Dispose();
        }
    }
}