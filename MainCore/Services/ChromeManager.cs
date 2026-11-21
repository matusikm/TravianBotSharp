using System.Collections.Concurrent;

using Serilog;

namespace MainCore.Services;

[RegisterSingleton<ChromeManager>]
public sealed class ChromeManager(IDbContextFactory<AppDbContext> contextFactory)
{
    private readonly ConcurrentDictionary<AccountId, IChromeBrowser> _dictionary = new();

    public IChromeBrowser Get(AccountId accountId)
    {
        // GetOrAdd guarantees every caller gets the same browser for an account.
        // Under a race the factory can run twice, but only one result is kept and
        // the other is never set up, so nothing leaks.
        return _dictionary.GetOrAdd(accountId, Create);
    }

    private ChromeBrowser Create(AccountId accountId)
    {
        var browser = new ChromeBrowser(contextFactory);
        browser.SetAccountId(accountId);
        browser.Logger = Log
            .ForContext("Account", GetAccountData(accountId))
            .ForContext("AccountId", accountId);
        return browser;
    }

    private string GetAccountData(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        return context.GetAccountData(accountId);
    }

    public async Task Shutdown()
    {
        foreach (var id in _dictionary.Keys)
        {
            if (_dictionary.Remove(id, out IChromeBrowser? browser))
            {
                await browser.Shutdown();
            }
        }
    }
}