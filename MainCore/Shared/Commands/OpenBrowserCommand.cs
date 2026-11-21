using System.Net;

namespace MainCore.Shared.Commands;

[Handler]
public sealed partial class OpenBrowserCommand(
    IDbContextFactory<AppDbContext> contextFactory,
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

        var (username, server, headless) = GetAccount(accountId);
        var chromeSetting = new ChromeSetting()
        {
            ProfilePath = GetProfilePath(server, username),
            UserAgent = access.UserAgent,
            ProxyHost = access.ProxyHost,
            ProxyPort = access.ProxyPort,
            ProxyUsername = access.ProxyUsername,
            ProxyPassword = access.ProxyPassword,
            IsHeadless = headless,
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

    private (string Username, string Server, bool headless) GetAccount(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        var account = context.Accounts
           .Where(x => x.Id == accountId.Value)
           .Select(x => new
           {
               x.Username,
               x.Server
           })
           .First();

        var headless = context.BooleanByName(accountId, AccountSettingEnums.HeadlessChrome);
        return (account.Username, account.Server, headless);
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
            using var client = CreateHttpClient(proxy);
            logger.Information("Checking proxy {Proxy}, last used {LastUsed}", proxy, proxy.LastUsed);
            try
            {
                using var response = await client.GetAsync(TRAVIAN_PAGE);
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

    private const string TRAVIAN_PAGE = "https://www.travian.com/international";

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