using MainCore.Shared.Commands;

namespace MainCore.Services;

/// <summary>Tells whether an account's connection to the internet works right now.</summary>
public interface INetworkProbe
{
    /// <summary>True when a test request through the account's connection (its proxy, or none) gets an answer.</summary>
    Task<bool> IsOnlineAsync(AccountId accountId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Asks the same neutral connectivity URL Chrome itself uses, through the proxy of the access the browser
/// was last opened with, so the answer matches what the browser sees and Travian never sees the request.
/// </summary>
[RegisterSingleton<INetworkProbe, NetworkProbe>]
public sealed class NetworkProbe(IDbContextFactory<AppDbContext> contextFactory) : INetworkProbe
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<bool> IsOnlineAsync(AccountId accountId, CancellationToken cancellationToken = default)
    {
        var access = GetLastUsedAccess(accountId) ?? NoProxy;

        using var client = new HttpClient(OpenBrowserCommand.CreateHttpClientHandler(access), disposeHandler: true)
        {
            Timeout = Timeout,
        };

        try
        {
            using var response = await client.GetAsync(OpenBrowserCommand.PROXY_CHECK_URL, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static readonly AccessDto NoProxy = new()
    {
        Username = "",
        Password = "",
        ProxyHost = "",
        ProxyUsername = "",
        ProxyPassword = "",
    };

    private AccessDto? GetLastUsedAccess(AccountId accountId)
    {
        using var context = contextFactory.CreateDbContext();
        return context.Accesses
            .Where(x => x.AccountId == accountId.Value)
            .OrderByDescending(x => x.LastUsed)
            .Select(x => new AccessDto
            {
                Id = new AccessId(x.Id),
                Username = "",
                Password = "",
                ProxyHost = x.ProxyHost,
                ProxyPort = x.ProxyPort,
                ProxyUsername = x.ProxyUsername,
                ProxyPassword = x.ProxyPassword,
            })
            .FirstOrDefault();
    }
}
