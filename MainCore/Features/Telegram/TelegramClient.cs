using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MainCore.Features.Telegram;

/// <summary>What happened to one message. <see cref="RetryAfter"/> is set when Telegram asked to slow down.</summary>
public sealed record TelegramSendResult(bool Ok, string Error = "", TimeSpan? RetryAfter = null, bool Permanent = false)
{
    public static TelegramSendResult Success { get; } = new(true);
}

/// <summary>A chat that recently wrote to the bot, as found by <see cref="ITelegramClient.FindChatsAsync"/>.</summary>
public sealed record TelegramChat(string Id, string Name);

public interface ITelegramClient
{
    Task<TelegramSendResult> SendMessageAsync(string token, string chatId, string text, CancellationToken cancellationToken = default);

    /// <summary>Chats that sent the bot a message lately (newest first), so the user doesn't have to look up the chat ID.</summary>
    Task<Result<IReadOnlyList<TelegramChat>>> FindChatsAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal Telegram Bot API client: sendMessage and getUpdates over HTTPS, plain text only.
/// The traffic goes straight to api.telegram.org, not through the account's proxy: Travian never sees it.
/// </summary>
[RegisterSingleton<ITelegramClient, TelegramClient>]
public sealed partial class TelegramClient : ITelegramClient
{
    public const string ApiBase = "https://api.telegram.org";

    /// <summary>Telegram's limit for one message.</summary>
    public const int MaxMessageLength = 4096;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;

    public TelegramClient() : this(new HttpClientHandler())
    {
    }

    public TelegramClient(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = RequestTimeout };
    }

    public async Task<TelegramSendResult> SendMessageAsync(string token, string chatId, string text, CancellationToken cancellationToken = default)
    {
        if (!IsValidToken(token)) return new(false, "The bot token doesn't look right. It's the text @BotFather gave you, like 123456:ABC-DEF...", Permanent: true);
        if (!IsValidChatId(chatId)) return new(false, "The chat ID should be a number (like 123456789 or -100123...) or @channelname", Permanent: true);

        if (text.Length > MaxMessageLength) text = text[..(MaxMessageLength - 1)] + "…";

        var body = new Dictionary<string, object>
        {
            ["chat_id"] = chatId.Trim(),
            ["text"] = text,
            ["disable_web_page_preview"] = true,
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/bot{token.Trim()}/sendMessage", body, cancellationToken);
            var reply = await ReadReply(response, cancellationToken);
            if (reply.Ok) return TelegramSendResult.Success;

            var retryAfter = reply.Parameters?.RetryAfter is int seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;
            // 400 (bad chat), 401 (bad token), 403 (bot blocked or not in the chat), 404 (no such bot) won't fix themselves.
            var permanent = reply.ErrorCode is 400 or 401 or 403 or 404;
            return new(false, Describe(reply), retryAfter, permanent);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            return new(false, Scrub(ex.Message, token));
        }
    }

    public async Task<Result<IReadOnlyList<TelegramChat>>> FindChatsAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!IsValidToken(token)) return Result.Fail("The bot token doesn't look right. It's the text @BotFather gave you, like 123456:ABC-DEF...");

        try
        {
            // Reading updates without an offset doesn't mark them as read, so other tools using the bot are unaffected.
            using var response = await _httpClient.GetAsync($"{ApiBase}/bot{token.Trim()}/getUpdates?limit=100", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var reply = JsonSerializer.Deserialize<UpdatesReply>(json);
            if (reply is null || !reply.Ok) return Result.Fail(reply is null ? "Telegram sent an empty reply" : Describe(reply));

            var chats = reply.Result
                .Select(x => x.Message?.Chat ?? x.ChannelPost?.Chat ?? x.MyChatMember?.Chat)
                .OfType<Chat>()
                .Reverse()
                .DistinctBy(x => x.Id)
                .Select(x => new TelegramChat(x.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), x.DisplayName))
                .ToList();
            return Result.Ok<IReadOnlyList<TelegramChat>>(chats);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            return Result.Fail(Scrub(ex.Message, token));
        }
    }

    public static bool IsValidToken(string? token) => !string.IsNullOrWhiteSpace(token) && TokenRegex().IsMatch(token.Trim());

    public static bool IsValidChatId(string? chatId) => !string.IsNullOrWhiteSpace(chatId) && ChatIdRegex().IsMatch(chatId.Trim());

    private static async Task<Reply> ReadReply(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<Reply>(json) ?? new Reply { ErrorCode = (int)response.StatusCode };
        }
        catch (JsonException)
        {
            return new Reply { ErrorCode = (int)response.StatusCode, Description = $"HTTP {(int)response.StatusCode}" };
        }
    }

    private static string Describe(Reply reply) => reply.ErrorCode switch
    {
        401 or 404 => "Telegram doesn't know this bot token. Copy it again from @BotFather",
        403 => "The bot can't write to this chat. Open the bot in Telegram and press Start, or add it to the group",
        400 when reply.Description.Contains("chat not found", StringComparison.OrdinalIgnoreCase) => "Chat not found. Send the bot a message first, then use Find chat",
        _ => string.IsNullOrEmpty(reply.Description) ? $"Telegram error {reply.ErrorCode}" : reply.Description,
    };

    // The token is part of the request URL; never let it reach the logs.
    private static string Scrub(string message, string token) =>
        string.IsNullOrWhiteSpace(token) ? message : message.Replace(token.Trim(), "<token>", StringComparison.Ordinal);

    [GeneratedRegex(@"^\d{5,}:[A-Za-z0-9_-]{30,}$")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^(-?\d{3,}|@[A-Za-z][A-Za-z0-9_]{4,})$")]
    private static partial Regex ChatIdRegex();

    private class Reply
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error_code")] public int ErrorCode { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("parameters")] public ReplyParameters? Parameters { get; set; }
    }

    private sealed class ReplyParameters
    {
        [JsonPropertyName("retry_after")] public int? RetryAfter { get; set; }
    }

    private sealed class UpdatesReply : Reply
    {
        [JsonPropertyName("result")] public List<Update> Result { get; set; } = [];
    }

    private sealed class Update
    {
        [JsonPropertyName("message")] public ChatHolder? Message { get; set; }
        [JsonPropertyName("channel_post")] public ChatHolder? ChannelPost { get; set; }
        [JsonPropertyName("my_chat_member")] public ChatHolder? MyChatMember { get; set; }
    }

    private sealed class ChatHolder
    {
        [JsonPropertyName("chat")] public Chat? Chat { get; set; }
    }

    private sealed class Chat
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }

        public string DisplayName => Title ?? FirstName ?? (Username is null ? Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : "@" + Username);
    }
}
