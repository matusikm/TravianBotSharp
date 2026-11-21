using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MainCore.Services;

public class ChromeSetting
{
    public required string ProfilePath { get; set; }
    public required string UserAgent { get; set; }
    public string? ProxyHost { get; set; }
    public int ProxyPort { get; set; }
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }
    public bool IsHeadless { get; set; }

    /// <summary>IANA time zone the browser reports, for example Asia/Bangkok. Null keeps this machine's zone.</summary>
    public string? TimeZoneId { get; set; }

    /// <summary>Browser language, for example en-US. Null keeps Chrome's default.</summary>
    public string? Locale { get; set; }
}

public sealed class ChromeBrowser(AccountSettingCache settingCache) : IChromeBrowser
{
    private IPlaywright? _playwright;
    private IBrowserContext? _browser;
    private IPage? _mainPage;

    public IPage CurrentPage => _mainPage ?? throw new InvalidOperationException("Main page is not initialized.");

    public string CurrentUrl => _mainPage?.Url ?? "";

    public ILogger Logger { get; set; } = null!;

    public bool IsInitialized => _playwright is not null && _browser is not null && _mainPage is not null;
    public bool IsHeadless { get; private set; }

    public AccountId AccountId { get; private set; }

    public void SetAccountId(AccountId accountId)
    {
        AccountId = accountId;
    }

    public async Task DelayClick()
    {
        var delay = settingCache.Skewed(AccountId, AccountSettingEnums.ClickDelayMin, AccountSettingEnums.ClickDelayMax);
        await Task.Delay(delay);
    }

    public async Task Setup(ChromeSetting setting)
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchPersistentContextAsync(GetPathUserData(setting), new()
        {
            Channel = "chrome",
            Headless = setting.IsHeadless,
            Proxy = string.IsNullOrEmpty(setting.ProxyHost) ? null : new()
            {
                Server = $"{setting.ProxyHost}:{setting.ProxyPort}",
                Username = setting.ProxyUsername,
                Password = setting.ProxyPassword
            },
            ViewportSize = ViewportSize.NoViewport,
            UserAgent = string.IsNullOrEmpty(setting.UserAgent) ? null : setting.UserAgent,
            TimezoneId = string.IsNullOrEmpty(setting.TimeZoneId) ? null : setting.TimeZoneId,
            Locale = string.IsNullOrEmpty(setting.Locale) ? null : setting.Locale,
            // No --disable-gpu: it makes WebGL report a software renderer, a common VM and bot fingerprint.
            Args =
            [
                "--no-default-browser-check",
                "--no-first-run",
                "--ash-no-nudges",
                "--mute-audio",
                "--disable-search-engine-choice-screen",
                "--webrtc-ip-handling-policy=disable_non_proxied_udp",
                "--force-webrtc-ip-handling-policy",
            ],
        });
        IsHeadless = setting.IsHeadless;
        _mainPage = await _browser.NewPageAsync();

        if (setting.IsHeadless)
        {
            Logger.Warning("Headless Chrome is easier to detect than a normal window. On Linux without a screen, run a normal window under Xvfb instead");
        }
        await WarnOnUserAgentMismatch(setting.UserAgent);

        var firstPage = _browser.Pages.Count > 1 ? _browser.Pages[0] : null;
        if (firstPage != null && firstPage.Url == "about:blank")
        {
            await firstPage.CloseAsync();
        }
    }

    private static string GetPathUserData(ChromeSetting setting)
    {
        var pathUserData = Path.Combine(AppContext.BaseDirectory, "Data", "Cache", setting.ProfilePath);
        if (!Directory.Exists(pathUserData)) Directory.CreateDirectory(pathUserData);
        pathUserData = Path.Combine(pathUserData, string.IsNullOrEmpty(setting.ProxyHost) ? "default" : setting.ProxyHost);
        return pathUserData;
    }

    public async Task Shutdown()
    {
        try
        {
            if (_mainPage is not null)
            {
                await _mainPage.CloseAsync();
                _mainPage = null;
            }
            if (_browser is not null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }
            if (_playwright is not null)
            {
                _playwright.Dispose();
                _playwright = null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during shutdown: {Message}", ex.Message);
        }
    }

    public async Task<string> Screenshot()
    {
        if (_mainPage is null)
        {
            Logger.Error("Screenshot failed: main page is null");
            return "";
        }

        var screenshot = await _mainPage.ScreenshotAsync();
        if (screenshot is null)
        {
            Logger.Error("Screenshot failed: screenshot is null");
            return "";
        }

        var fileName = Path.Combine(AppContext.BaseDirectory, "Screenshots", $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
        await File.WriteAllBytesAsync(fileName, screenshot ?? [], CancellationToken.None);
        return fileName;
    }

    public async Task<Result> Refresh()
    {
        if (_mainPage is null)
        {
            Logger.Error("Refresh failed: main page is null");
            return Stop.Error;
        }
        await _mainPage.ReloadAsync();
        return Result.Ok();
    }

    public async Task<Result> Navigate(string url)
    {
        if (_mainPage is null)
        {
            Logger.Error("Navigate failed: main page is null");
            return Stop.Error;
        }

        try
        {
            await _mainPage.GotoAsync(url);
            return Result.Ok();
        }
        catch (TimeoutException ex)
        {
            Logger.Error(ex, "Navigating to URL [{Url}] timed out.", url);
            return Retry.Error;
        }
    }

    public async Task<Result> Click(ILocator locator, float? timeout = null, [CallerArgumentExpression(nameof(locator))] string? expression = null)
    {
        if (_mainPage is null)
        {
            Logger.Error("Click failed: main page is null");
            return Stop.Error;
        }
        try
        {
            await HumanClick(_mainPage, locator, timeout);
            await DelayClick();
            return Result.Ok();
        }
        catch (TimeoutException ex)
        {
            Logger.Error(ex, "Clicking locator [{Expression}] timed out.", expression);
            return Retry.Error;
        }
    }

    /// <summary>
    /// Moves the mouse to a random point inside the element in several steps, pauses briefly, then clicks there,
    /// instead of jumping to the element's exact centre. Falls back to a plain click when the element has no box.
    /// </summary>
    private static async Task HumanClick(IPage page, ILocator locator, float? timeout)
    {
        await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = timeout });
        var box = await locator.BoundingBoxAsync(new() { Timeout = timeout });
        if (box is null)
        {
            await locator.ClickAsync(new() { Timeout = timeout });
            return;
        }

        var random = System.Random.Shared;
        var (x, y) = HumanTiming.PointInBox(random, box.Width, box.Height);
        await page.Mouse.MoveAsync(box.X + x, box.Y + y, new() { Steps = random.Next(8, 26) });
        await Task.Delay(HumanTiming.SkewedDelay(random, 40, 220));
        await locator.ClickAsync(new() { Position = new() { X = x, Y = y }, Timeout = timeout });
    }

    public async Task<Result> Input(ILocator locator, string content, [CallerArgumentExpression(nameof(locator))] string? expression = null)
    {
        if (_mainPage is null)
        {
            Logger.Error("Input failed: main page is null");
            return Stop.Error;
        }
        try
        {
            await HumanType(_mainPage, locator, content);
            await DelayClick();
            return Result.Ok();
        }
        catch (TimeoutException ex)
        {
            Logger.Error(ex, "Inputting into locator [{Expression}] timed out.", expression);
            return Retry.Error;
        }
    }

    /// <summary>
    /// Clicks into the field, selects what is there and types the new text one key at a time with uneven pauses,
    /// instead of setting the whole value at once.
    /// </summary>
    private static async Task HumanType(IPage page, ILocator locator, string content)
    {
        await HumanClick(page, locator, null);
        await page.Keyboard.PressAsync("ControlOrMeta+A");
        await Task.Delay(HumanTiming.KeyDelay(System.Random.Shared));

        if (content.Length == 0)
        {
            await page.Keyboard.PressAsync("Backspace");
            return;
        }

        foreach (var character in content)
        {
            await page.Keyboard.TypeAsync(character.ToString());
            await Task.Delay(HumanTiming.KeyDelay(System.Random.Shared));
        }
    }

    private async Task WarnOnUserAgentMismatch(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent) || _mainPage is null) return;

        try
        {
            var installedVersion = await _mainPage.EvaluateAsync<string?>(
                "() => navigator.userAgentData?.brands?.find(b => b.brand === 'Google Chrome')?.version ?? null");
            if (!UserAgentCheck.Matches(userAgent, installedVersion))
            {
                Logger.Warning("The custom User-Agent claims Chrome {Claimed} but the installed Chrome is {Installed}. Sites can see both; leave the User-Agent empty or update it",
                    UserAgentCheck.ChromeMajorVersion(userAgent)?.ToString() ?? "unknown", installedVersion ?? "unknown");
            }
        }
        catch (PlaywrightException ex)
        {
            Logger.Debug(ex, "Could not compare User-Agent with installed Chrome");
        }
    }

    public async Task<Result> ExecuteJsScript(string javascript)
    {
        if (_mainPage is null)
        {
            Logger.Error("ExecuteJsScript failed: main page is null");
            return Stop.Error;
        }
        await _mainPage.EvaluateAsync(javascript);
        await DelayClick();
        return Result.Ok();
    }

    public async Task<Result> Wait(ILocator locator, [CallerArgumentExpression(nameof(locator))] string? expression = null)
    {
        if (_mainPage is null)
        {
            Logger.Error("Wait failed: main page is null");
            return Stop.Error;
        }
        try
        {
            const int timeout = 10_000; // 10 seconds in milliseconds
            await locator.WaitForAsync(new() { Timeout = timeout });
            return Result.Ok();
        }
        catch (TimeoutException ex)
        {
            Logger.Error(ex, "Waiting for locator [{Expression}] timed out.", expression);
            return Retry.Error;
        }
    }

    public async Task<Result> Wait(ILocator locator, string condition, [CallerArgumentExpression(nameof(locator))] string? expression = null)
    {
        if (_mainPage is null)
        {
            Logger.Error("Wait failed: main page is null");
            return Stop.Error;
        }
        try
        {
            await locator.WaitForFunctionAsync(condition);
            return Result.Ok();
        }
        catch (TimeoutException ex)
        {
            Logger.Error(ex, "Waiting for locator [{Expression}] timed out.", expression);
            return Retry.Error;
        }
    }

    public async Task<Result> WaitPageChanged(string url)
    {
        if (_mainPage is null)
        {
            Logger.Error("WaitPageChanged failed: main page is null");
            return Stop.Error;
        }

        try
        {
            const int timeout = 180_000; // 3 minutes in milliseconds
            await _mainPage.WaitForURLAsync(new Regex(url, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)), new() { Timeout = timeout });
            await _mainPage.WaitForLoadStateAsync();
            await _mainPage.GetServerTime().WaitForAsync(new() { Timeout = timeout });
            return Result.Ok();
        }
        catch (TimeoutException ex)
        {
            string actualUrl = _mainPage.Url;
            Logger.Error(ex, "Navigation or page load failed. Expected URL: [{Url}]. Current URL: [{ActualUrl}]", url, actualUrl);
            return Retry.Error;
        }
    }
}