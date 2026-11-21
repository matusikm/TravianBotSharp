using System.Net;
using System.Text;

using FluentResults;

using MainCore.Features.Telegram;
using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Infrasturecture.Security;
using MainCore.Services;
using MainCore.Shared.Dtos;
using MainCore.Shared.Enums;
using MainCore.Shared.Errors;
using MainCore.Shared.Notifications;
using MainCore.UI.Models.Input;
using MainCore.UI.Models.Validators;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MainCore.Test;

public class TelegramAlertRulesTests
{
    private static readonly AccountId Account = new(1);

    [Fact]
    public void Attack_IsReportedOnce_AndAgainAfterItClears()
    {
        var rules = new TelegramAlertRules();
        ObservedVillage Village(bool attacked) => new(10, "Home", 1, -2, attacked);

        rules.NewAttacks(Account, [Village(true)]).Single().Name.ShouldBe("Home");
        rules.NewAttacks(Account, [Village(true)]).ShouldBeEmpty();
        rules.NewAttacks(Account, [Village(false)]).ShouldBeEmpty();
        rules.NewAttacks(Account, [Village(true)]).Count.ShouldBe(1);
    }

    [Fact]
    public void Attack_IsTrackedPerAccount()
    {
        var rules = new TelegramAlertRules();
        var village = new ObservedVillage(10, "Home", 1, -2, true);

        rules.NewAttacks(new AccountId(1), [village]).Count.ShouldBe(1);
        rules.NewAttacks(new AccountId(2), [village]).Count.ShouldBe(1);
    }

    [Fact]
    public void Storage_IsReportedOnceAt90Percent_AndRearmsAt80()
    {
        var rules = new TelegramAlertRules();
        var village = new VillageId(10);
        StorageDto Storage(long wood, long crop) => new() { Wood = wood, Clay = 0, Iron = 0, Crop = crop, Warehouse = 1000, Granary = 2000 };

        rules.NearlyFull(Account, village, Storage(899, 0)).ShouldBeEmpty();
        rules.NearlyFull(Account, village, Storage(950, 1900)).ShouldBe([("wood", 95), ("crop", 95)]);
        rules.NearlyFull(Account, village, Storage(990, 1900)).ShouldBeEmpty();
        rules.NearlyFull(Account, village, Storage(850, 0)).ShouldBeEmpty(); // between 80 and 90: still armed off
        rules.NearlyFull(Account, village, Storage(950, 0)).ShouldBeEmpty();
        rules.NearlyFull(Account, village, Storage(800, 0)).ShouldBeEmpty(); // re-arms
        rules.NearlyFull(Account, village, Storage(900, 0)).ShouldBe([("wood", 90)]);
    }

    [Fact]
    public void Storage_WithUnknownCapacity_IsIgnored()
    {
        var rules = new TelegramAlertRules();

        rules.NearlyFull(Account, new VillageId(1), new StorageDto { Wood = 5, Warehouse = 0, Granary = 0 }).ShouldBeEmpty();
    }
}

public class TelegramClientTests
{
    private const string Token = "123456789:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw1";

    private sealed class FakeHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];
        public Exception? Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), body));
            if (Throw is not null) throw Throw;
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Send_PostsToTheBotsChat()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"ok":true,"result":{}}""");
        var client = new TelegramClient(handler);

        var result = await client.SendMessageAsync(Token, "123456789", "Incoming attack");

        result.Ok.ShouldBeTrue();
        var request = handler.Requests.Single();
        request.Url.ShouldBe($"https://api.telegram.org/bot{Token}/sendMessage");
        request.Body.ShouldContain("\"chat_id\":\"123456789\"");
        request.Body.ShouldContain("Incoming attack");
    }

    [Fact]
    public async Task Send_RateLimited_ReturnsRetryAfter()
    {
        var handler = new FakeHandler(HttpStatusCode.TooManyRequests,
            """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 7","parameters":{"retry_after":7}}""");
        var client = new TelegramClient(handler);

        var result = await client.SendMessageAsync(Token, "123456789", "x");

        result.Ok.ShouldBeFalse();
        result.Permanent.ShouldBeFalse();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task Send_BotBlocked_IsPermanent()
    {
        var handler = new FakeHandler(HttpStatusCode.Forbidden,
            """{"ok":false,"error_code":403,"description":"Forbidden: bot was blocked by the user"}""");
        var client = new TelegramClient(handler);

        var result = await client.SendMessageAsync(Token, "123456789", "x");

        result.Permanent.ShouldBeTrue();
        result.Error.ShouldContain("press Start");
    }

    [Fact]
    public async Task Send_NetworkError_DoesNotLeakTheToken()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "") { Throw = new HttpRequestException($"failed to reach /bot{Token}/sendMessage") };
        var client = new TelegramClient(handler);

        var result = await client.SendMessageAsync(Token, "123456789", "x");

        result.Ok.ShouldBeFalse();
        result.Permanent.ShouldBeFalse();
        result.Error.ShouldNotContain(Token);
    }

    [Theory]
    [InlineData("", "123456789")]
    [InlineData("not-a-token", "123456789")]
    [InlineData(Token, "")]
    [InlineData(Token, "chat")]
    public async Task Send_BadSettings_FailWithoutARequest(string token, string chatId)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"ok":true}""");
        var client = new TelegramClient(handler);

        var result = await client.SendMessageAsync(token, chatId, "x");

        result.Permanent.ShouldBeTrue();
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("123456789", true)]
    [InlineData("-1001234567890", true)]
    [InlineData("@my_channel", true)]
    [InlineData("12", false)]
    [InlineData("@ab", false)]
    public void ChatId_Format(string chatId, bool valid)
    {
        TelegramClient.IsValidChatId(chatId).ShouldBe(valid);
    }

    [Fact]
    public async Task FindChats_ReturnsNewestChatFirst()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            {"ok":true,"result":[
              {"update_id":1,"message":{"chat":{"id":111,"first_name":"Martin","type":"private"},"text":"/start"}},
              {"update_id":2,"message":{"chat":{"id":-100222,"title":"Travian alerts","type":"supergroup"},"text":"hi"}},
              {"update_id":3,"message":{"chat":{"id":111,"first_name":"Martin","type":"private"},"text":"again"}}
            ]}
            """);
        var client = new TelegramClient(handler);

        var result = await client.FindChatsAsync(Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Id).ShouldBe(["111", "-100222"]);
        result.Value[1].Name.ShouldBe("Travian alerts");
        handler.Requests.Single().Url.ShouldStartWith($"https://api.telegram.org/bot{Token}/getUpdates");
    }
}

[Collection(nameof(SecretProtectionCollection))]
public sealed class TelegramNotifierTests : IDisposable
{
    private const string Token = "123456789:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw1";

    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), "tbs-test-" + Guid.NewGuid().ToString("N"));
    private readonly ISecretProtector _previous;
    private readonly SqliteConnection _connection;
    private readonly Factory _factory;
    private readonly FakeClient _client = new();
    private readonly AccountId _accountId;

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    }

    private sealed class FakeClient : ITelegramClient
    {
        public List<(string Token, string ChatId, string Text)> Sent { get; } = [];

        public Task<TelegramSendResult> SendMessageAsync(string token, string chatId, string text, CancellationToken cancellationToken = default)
        {
            lock (Sent) Sent.Add((token, chatId, text));
            return Task.FromResult(TelegramSendResult.Success);
        }

        public Task<Result<IReadOnlyList<TelegramChat>>> FindChatsAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Ok<IReadOnlyList<TelegramChat>>([]));
    }

    public TelegramNotifierTests()
    {
        _previous = SecretProtection.Current;
        SecretProtection.Current = new AesGcmSecretProtector(Path.Combine(_keyDirectory, "secret.key"));

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _factory = new Factory(_connection);

        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
        var account = new Account
        {
            Username = "martin",
            Server = "https://ts1.x1.europe.travian.com",
            TelegramBotToken = Token,
            TelegramChatId = "123456789",
        };
        context.Accounts.Add(account);
        context.SaveChanges();
        _accountId = new AccountId(account.Id);
        context.FillAccountSettings();
        context.Villages.Add(new Village { Id = 77, AccountId = account.Id, Name = "Home", X = 5, Y = -7, IsUnderAttack = true });
        context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SecretProtection.Current = _previous;
        if (Directory.Exists(_keyDirectory)) Directory.Delete(_keyDirectory, recursive: true);
    }

    private TelegramNotifier CreateNotifier(bool enabled)
    {
        var cache = new AccountSettingCache(_factory);
        cache.Save(_accountId, new() { [AccountSettingEnums.EnableTelegram] = enabled ? 1 : 0 });
        return new TelegramNotifier(_factory, cache, _client, TimeProvider.System);
    }

    private async Task<List<(string Token, string ChatId, string Text)>> WaitForMessages(int count)
    {
        for (var i = 0; i < 50; i++)
        {
            lock (_client.Sent) if (_client.Sent.Count >= count) return [.. _client.Sent];
            await Task.Delay(100);
        }
        lock (_client.Sent) return [.. _client.Sent];
    }

    [Fact]
    public void Token_IsEncryptedInTheDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT TelegramBotToken FROM Accounts";
        var stored = (string)command.ExecuteScalar()!;

        stored.ShouldStartWith(AesGcmSecretProtector.Prefix);
        stored.ShouldNotContain(Token);
    }

    [Fact]
    public async Task Paused_SendsALabelledMessage()
    {
        using var notifier = CreateNotifier(enabled: true);

        notifier.OnAccountNeedsAttention(new AccountNeedsAttention(_accountId, "Train troop stopped: no barracks"));

        var sent = (await WaitForMessages(1)).Single();
        sent.Token.ShouldBe(Token);
        sent.ChatId.ShouldBe("123456789");
        sent.Text.ShouldBe("[martin @ ts1.x1.europe.travian.com] Bot paused: Train troop stopped: no barracks. Check the account and press Resume.");
    }

    [Fact]
    public async Task Attack_IsSentOnce()
    {
        using var notifier = CreateNotifier(enabled: true);

        notifier.OnVillagesModified(new VillagesModified(_accountId));
        notifier.OnVillagesModified(new VillagesModified(_accountId));

        var sent = await WaitForMessages(2);
        sent.Single().Text.ShouldEndWith("Incoming attack on Home (5|-7).");
    }

    [Fact]
    public async Task StorageNearlyFull_NamesTheVillage()
    {
        using var notifier = CreateNotifier(enabled: true);

        notifier.OnStorageModified(new StorageModified(_accountId, new VillageId(77),
            new StorageDto { Wood = 10, Clay = 10, Iron = 10, Crop = 1950, Warehouse = 1000, Granary = 2000 }));

        (await WaitForMessages(1)).Single().Text.ShouldEndWith("Storage nearly full in Home: crop 97%.");
    }

    [Fact]
    public async Task SwitchedOff_SendsNothing()
    {
        using var notifier = CreateNotifier(enabled: false);

        notifier.OnAccountNeedsAttention(new AccountNeedsAttention(_accountId, "x"));
        notifier.OnVillagesModified(new VillagesModified(_accountId));

        await Task.Delay(500);
        _client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task EventSwitchedOff_SendsNothingForIt()
    {
        var cache = new AccountSettingCache(_factory);
        cache.Save(_accountId, new()
        {
            [AccountSettingEnums.EnableTelegram] = 1,
            [AccountSettingEnums.TelegramNotifyAttack] = 0,
        });
        using var notifier = new TelegramNotifier(_factory, cache, _client, TimeProvider.System);

        notifier.OnVillagesModified(new VillagesModified(_accountId));

        await Task.Delay(500);
        _client.Sent.ShouldBeEmpty();
    }
}

public class TelegramSettingsTests
{
    [Fact]
    public void Validator_TelegramOnWithoutToken_Fails()
    {
        var input = new AccountSettingInput { EnableTelegram = true, TelegramChatId = "123456789" };
        input.WorkTime.Set(8, 10);

        var result = new AccountSettingInputValidator().Validate(input);

        result.Errors.ShouldContain(x => x.PropertyName == nameof(AccountSettingInput.TelegramBotToken));
    }

    [Fact]
    public void Validator_TelegramOff_IgnoresEmptyToken()
    {
        var input = new AccountSettingInput();

        var result = new AccountSettingInputValidator().Validate(input);

        result.Errors.ShouldNotContain(x => x.PropertyName.StartsWith("Telegram"));
    }

    [Fact]
    public void Settings_RoundTripThroughTheInput()
    {
        var input = new AccountSettingInput();
        input.Set(new()
        {
            [AccountSettingEnums.EnableTelegram] = 1,
            [AccountSettingEnums.TelegramNotifyAttack] = 1,
            [AccountSettingEnums.TelegramNotifyPaused] = 0,
            [AccountSettingEnums.TelegramNotifyStorage] = 1,
        });

        var settings = input.Get();

        settings[AccountSettingEnums.EnableTelegram].ShouldBe(1);
        settings[AccountSettingEnums.TelegramNotifyPaused].ShouldBe(0);
        settings[AccountSettingEnums.TelegramNotifyStorage].ShouldBe(1);
    }

    [Fact]
    public void PausedReason_UsesTheSpecificErrors()
    {
        var result = Stop.Error.WithError("Barracks is missing");

        TimerManager.DescribeErrors(result).ShouldBe("Barracks is missing");
        TimerManager.DescribeErrors(Retry.Error).ShouldBe("it kept failing after retries");
    }
}
