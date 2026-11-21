using System.Threading.Channels;

using Serilog;

namespace MainCore.Features.Telegram;

/// <summary>
/// Sends Telegram messages for an account when its Telegram switch is on: a village gets attacked, the bot pauses
/// on its own, or a warehouse or granary is nearly full. Messages wait in a small in-memory queue and are sent one
/// at a time in the background, so a slow or unreachable Telegram never holds up the browser.
/// </summary>
[RegisterSingleton<TelegramNotifier>]
public sealed class TelegramNotifier : IDisposable
{
    /// <summary>At most this many messages wait; when more arrive, the oldest are dropped.</summary>
    public const int QueueCapacity = 50;

    /// <summary>A message that couldn't be sent within this time is dropped instead of arriving late.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    /// <summary>Gap between two messages; Telegram allows about one message per second to the same chat.</summary>
    public static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1.1);

    private const int MaxAttempts = 3;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly AccountSettingCache _settingCache;
    private readonly ITelegramClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly TelegramAlertRules _rules = new();
    private readonly Channel<Pending> _queue = Channel.CreateBounded<Pending>(new BoundedChannelOptions(QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    });
    private readonly CancellationTokenSource _stop = new();

    private sealed record Pending(AccountId AccountId, string Text, DateTimeOffset CreatedAt);

    public TelegramNotifier(IDbContextFactory<AppDbContext> contextFactory, AccountSettingCache settingCache, ITelegramClient client, TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _settingCache = settingCache;
        _client = client;
        _timeProvider = timeProvider;
        _ = Task.Run(SendLoop);
    }

    /// <summary>Hooks the notifier to the bot's events. Called once at start-up.</summary>
    public void Register(RxQueue rxQueue)
    {
        rxQueue.RegisterHandler<VillagesModified>(OnVillagesModified);
        rxQueue.RegisterHandler<StorageModified>(OnStorageModified);
        rxQueue.RegisterHandler<AccountNeedsAttention>(OnAccountNeedsAttention);
    }

    public void OnVillagesModified(VillagesModified notification)
    {
        var accountId = notification.AccountId;
        if (!IsEnabled(accountId)) return;

        List<ObservedVillage> villages;
        using (var context = _contextFactory.CreateDbContext())
        {
            villages = context.Villages
                .Where(x => x.AccountId == accountId.Value)
                .Select(x => new ObservedVillage(x.Id, x.Name, x.X, x.Y, x.IsUnderAttack))
                .ToList();
        }

        var attacked = _rules.NewAttacks(accountId, villages);
        if (attacked.Count == 0 || !IsOn(accountId, AccountSettingEnums.TelegramNotifyAttack)) return;

        var names = string.Join(", ", attacked.Select(x => $"{x.Name} ({x.X}|{x.Y})"));
        var text = attacked.Count == 1
            ? $"Incoming attack on {names}."
            : $"Incoming attacks on {attacked.Count} villages: {names}.";
        Enqueue(accountId, text);
    }

    public void OnStorageModified(StorageModified notification)
    {
        var accountId = notification.AccountId;
        if (!IsEnabled(accountId)) return;

        var full = _rules.NearlyFull(accountId, notification.VillageId, notification.Storage);
        if (full.Count == 0 || !IsOn(accountId, AccountSettingEnums.TelegramNotifyStorage)) return;

        string villageName;
        using (var context = _contextFactory.CreateDbContext())
        {
            villageName = context.Villages
                .Where(x => x.Id == notification.VillageId.Value)
                .Select(x => x.Name)
                .FirstOrDefault() ?? $"village {notification.VillageId.Value}";
        }

        var resources = string.Join(", ", full.Select(x => $"{x.Resource} {x.Percent}%"));
        Enqueue(accountId, $"Storage nearly full in {villageName}: {resources}.");
    }

    public void OnAccountNeedsAttention(AccountNeedsAttention notification)
    {
        var accountId = notification.AccountId;
        if (!IsEnabled(accountId) || !IsOn(accountId, AccountSettingEnums.TelegramNotifyPaused)) return;

        Enqueue(accountId, $"Bot paused: {notification.Reason}. Check the account and press Resume.");
    }

    /// <summary>Sends one message right away with the given token and chat, for the settings tab's test button.</summary>
    public async Task<Result> SendTestAsync(AccountId accountId, string token, string chatId)
    {
        var text = $"{GetLabel(accountId)} Test message from TravianBotSharp. Telegram notifications work.";
        var result = await _client.SendMessageAsync(token, chatId, text);
        return result.Ok ? Result.Ok() : Result.Fail(result.Error);
    }

    private bool IsEnabled(AccountId accountId) => _settingCache.Get(accountId, AccountSettingEnums.EnableTelegram) == 1;

    private bool IsOn(AccountId accountId, AccountSettingEnums setting) => _settingCache.Get(accountId, setting) == 1;

    private void Enqueue(AccountId accountId, string text)
    {
        _queue.Writer.TryWrite(new Pending(accountId, text, _timeProvider.GetUtcNow()));
    }

    private async Task SendLoop()
    {
        var token = _stop.Token;
        try
        {
            await foreach (var pending in _queue.Reader.ReadAllAsync(token))
            {
                try
                {
                    await Send(pending, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.ForContext("AccountId", pending.AccountId).Warning("Telegram message not sent: {Message}", ex.Message);
                }
                await Task.Delay(SendInterval, _timeProvider, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task Send(Pending pending, CancellationToken cancellationToken)
    {
        var logger = Log.ForContext("AccountId", pending.AccountId);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // Settings are read again at send time: a switched-off account or a changed chat gets nothing stale.
            if (!IsEnabled(pending.AccountId)) return;
            if (_timeProvider.GetUtcNow() - pending.CreatedAt > MaxAge)
            {
                logger.Warning("Telegram message dropped, it couldn't be sent within {MaxAge}", MaxAge.Humanize());
                return;
            }

            var (botToken, chatId) = GetDestination(pending.AccountId);
            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            {
                logger.Warning("Telegram is on, but the bot token or chat ID is missing in the account settings");
                return;
            }

            var result = await _client.SendMessageAsync(botToken, chatId, $"{GetLabel(pending.AccountId)} {pending.Text}", cancellationToken);
            if (result.Ok) return;

            if (result.Permanent || attempt == MaxAttempts)
            {
                logger.Warning("Telegram message not sent: {Error}", result.Error);
                return;
            }

            var wait = result.RetryAfter is { } retryAfter && retryAfter <= TimeSpan.FromMinutes(5)
                ? retryAfter
                : TimeSpan.FromSeconds(10 * attempt);
            await Task.Delay(wait, _timeProvider, cancellationToken);
        }
    }

    private (string? Token, string? ChatId) GetDestination(AccountId accountId)
    {
        using var context = _contextFactory.CreateDbContext();
        var account = context.Accounts
            .Where(x => x.Id == accountId.Value)
            .Select(x => new { x.TelegramBotToken, x.TelegramChatId })
            .FirstOrDefault();
        return (account?.TelegramBotToken, account?.TelegramChatId);
    }

    /// <summary>"[username @ server]", so one chat can serve several accounts.</summary>
    private string GetLabel(AccountId accountId)
    {
        using var context = _contextFactory.CreateDbContext();
        var account = context.Accounts
            .Where(x => x.Id == accountId.Value)
            .Select(x => new { x.Username, x.Server })
            .FirstOrDefault();
        if (account is null) return "[TBS]";

        var server = Uri.TryCreate(account.Server, UriKind.Absolute, out var uri) ? uri.Host : account.Server;
        return $"[{account.Username} @ {server}]";
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
