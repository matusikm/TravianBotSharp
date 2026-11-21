using System.Net;

namespace MainCore.Shared.Commands;

[Handler]
public sealed partial class OpenBrowserCommand(
    IDbContextFactory<AppDbContext> contextFactory,
    AccountTimeZoneStore timeZoneStore,
    TimeProvider timeProvider,
    ILogger logger)
{
    public sealed record Command(IChromeBrowser Browser, bool IgnoreSleepTime = false) : IBrowserCommand
    {
        public string Name => $"{nameof(OpenBrowserCommand)}";
    }

    private async ValueTask<Result> HandleAsync(Command command)
    {
        var (browser, ignoreSleepTime) = command;
        var accountId = browser.AccountId;

        var result = await GetValidAccess(accountId, ignoreSleepTime);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var access = result.Value;

        var (username, server, headless, timeZone, language) = GetAccount(accountId);
        var locale = await ResolveLocale(access, timeZone, language);
        timeZoneStore.Set(accountId, locale.TimeZone);
        logger.Information("Browser time zone {TimeZone}, language {Language}", locale.TimeZoneId, locale.Locale ?? "Chrome default");

        var chromeSetting = new ChromeSetting()
        {
            ProfilePath = GetProfilePath(server, username),
            UserAgent = access.UserAgent,
            ProxyHost = access.ProxyHost,
            ProxyPort = access.ProxyPort,
            ProxyUsername = access.ProxyUsername,
            ProxyPassword = access.ProxyPassword,
            IsHeadless = headless,
            TimeZoneId = locale.TimeZoneId,
            Locale = locale.Locale,
        };

        await browser.Setup(chromeSetting);
        await browser.Navigate($"{server}");

        UpdateLastUsed(access);
        return Result.Ok();
    }

    private static string GetProfilePath(string server, string username)
    {
        var uri = new Uri(server);
        var serverFolderName = uri.Host.Replace(".", "_");
        return Path.Combine(serverFolderName, username);
    }

    private (string Username, string Server, bool headless, string? TimeZone, string? Language) GetAccount(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        var account = context.Accounts
           .Where(x => x.Id == accountId.Value)
           .Select(x => new
           {
               x.Username,
               x.Server,
               x.TimeZone,
               x.Language,
           })
           .First();

        var headless = context.BooleanByName(accountId, AccountSettingEnums.HeadlessChrome);
        return (account.Username, account.Server, headless, account.TimeZone, account.Language);
    }

    private async Task<ResolvedLocale> ResolveLocale(AccessDto access, string? timeZone, string? language)
    {
        GeoInfo? geo = null;
        if (AccountLocale.NeedsLookup(timeZone, language))
        {
            geo = await LookupGeo(access);
            if (geo is null)
            {
                logger.Warning("Could not look up the current IP's location; using this computer's time zone");
            }
        }

        return AccountLocale.Resolve(timeZone, language, geo, timeProvider.LocalTimeZone);
    }

    // Looked up from the same connection the browser will use (the access's proxy, or no proxy),
    // so the browser's time zone and language match the IP that Travian sees.
    private async Task<GeoInfo?> LookupGeo(AccessDto access)
    {
        foreach (var url in GEO_LOOKUP_URLS)
        {
            try
            {
                using var client = CreateHttpClient(access);
                var json = await client.GetStringAsync(url);
                var geo = AccountLocale.ParseGeoJson(json);
                if (geo is not null) return geo;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.Warning(ex, "Location lookup via {Url} failed: {Message}", url, ex.Message);
            }
        }
        return null;
    }

    private void UpdateLastUsed(AccessDto access)
    {
        using var context = contextFactory.CreateDbContext();
        context.Accesses
           .Where(x => x.Id == access.Id.Value)
           .ExecuteUpdate(x => x.SetProperty(x => x.LastUsed, x => DateTime.Now));
    }

    private List<AccessDto> GetAccesses(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        var accesses = context.Accesses
          .Where(x => x.AccountId == accountId.Value)
          .OrderBy(x => x.LastUsed)
          .Select(x => new AccessDto
          {
              Id = new AccessId(x.Id),
              Username = x.Username,
              Password = x.Password,
              ProxyHost = x.ProxyHost,
              ProxyPort = x.ProxyPort,
              ProxyUsername = x.ProxyUsername,
              ProxyPassword = x.ProxyPassword,
              UserAgent = x.Useragent,
              LastUsed = x.LastUsed
          })
          .ToList();
        return accesses;
    }

    private int GetSleepTime(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        return context.ByName(accountId, AccountSettingEnums.SleepTimeMin);
    }

    private async Task<Result<AccessDto>> GetValidAccess(AccountId accountId, bool ignoreSleepTime)
    {
        var accesses = GetAccesses(accountId);

        var access = await GetValidAccess(accesses);
        if (access is null)
        {
            logger.Error("All accesses not working");
            return Stop.Error;
        }

        if (accesses.Count == 1) return access;
        if (ignoreSleepTime) return access;

        var minSleep = GetSleepTime(accountId);
        var timeValid = DateTime.Now.AddMinutes(-minSleep);
        if (access.LastUsed > timeValid)
        {
            logger.Error("Last access is reused, it may get MH's attention");
            return Stop.Error;
        }

        logger.Information("Using connection {Proxy} to start chrome", access);
        return access;
    }

    private async Task<AccessDto?> GetValidAccess(List<AccessDto> proxies)
    {
        foreach (var proxy in proxies)
        {
            // Without a proxy there is nothing to test, and a request would only add noise from this IP.
            if (string.IsNullOrEmpty(proxy.ProxyHost)) return proxy;

            using var client = CreateHttpClient(proxy);
            logger.Information("Checking proxy {Proxy}, last used {LastUsed}", proxy, proxy.LastUsed);
            try
            {
                using var response = await client.GetAsync(PROXY_CHECK_URL);
                if (response.IsSuccessStatusCode)
                {
                    logger.Information("Access {Proxy} is good", proxy);
                    return proxy;
                }

                logger.Warning("Access {Proxy} is not working, status code: {StatusCode}", proxy, response.StatusCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "{Message}", ex.Message);
            }
        }
        return null;
    }

    private static readonly TimeSpan ProxyCheckTimeout = TimeSpan.FromSeconds(15);

    // A neutral connectivity URL (the one Chrome itself uses), so the check never shows Travian
    // a header-less, cookie-less request from the account's proxy IP right before each login.
    private static readonly string[] GEO_LOOKUP_URLS = ["https://ipinfo.io/json", "https://ipapi.co/json/"];

    public const string PROXY_CHECK_URL = "https://www.gstatic.com/generate_204";

    // Each check gets its own handler and proxy object. A shared static proxy was
    // overwritten per account, so two accounts starting together could check through
    // each other's proxy and send one account's proxy password to another proxy.
    public static HttpClientHandler CreateHttpClientHandler(AccessDto access)
    {
        if (string.IsNullOrEmpty(access.ProxyHost))
        {
            return new HttpClientHandler { UseProxy = false };
        }

#pragma warning disable S5332 // Clear-text protocols should not be used
        var proxy = new WebProxy(new Uri($"http://{access.ProxyHost}:{access.ProxyPort}"));
#pragma warning restore S5332 // Clear-text protocols should not be used

        if (!string.IsNullOrEmpty(access.ProxyUsername))
        {
            proxy.Credentials = new NetworkCredential(access.ProxyUsername, access.ProxyPassword);
        }

        return new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true,
        };
    }

    private static HttpClient CreateHttpClient(AccessDto access)
    {
        return new HttpClient(CreateHttpClientHandler(access), disposeHandler: true)
        {
            Timeout = ProxyCheckTimeout,
        };
    }
}