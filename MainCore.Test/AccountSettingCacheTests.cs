using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Services;
using MainCore.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MainCore.Test;

public sealed class AccountSettingCacheTests : IDisposable
{
    private static readonly AccountId Account = new(1);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly AccountSettingCache _cache;

    public AccountSettingCacheTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using (var context = new AppDbContext(_options))
        {
            context.Database.EnsureCreated();
            context.Database.ExecuteSqlRaw("""
                INSERT INTO Accounts (Id, Username, Server) VALUES (1, 'player', 'https://ts1.x1.europe.travian.com');
                INSERT INTO AccountsSetting (AccountId, Setting, Value) VALUES (1, {0}, 500), (1, {1}, 900);
                """, (int)AccountSettingEnums.ClickDelayMin, (int)AccountSettingEnums.ClickDelayMax);
        }

        var factory = Substitute.For<IDbContextFactory<AppDbContext>>();
        factory.CreateDbContext().Returns(_ => new AppDbContext(_options));
        _cache = new AccountSettingCache(factory);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Random_StaysWithinTheRange()
    {
        for (var i = 0; i < 100; i++)
        {
            _cache.Random(Account, AccountSettingEnums.ClickDelayMin, AccountSettingEnums.ClickDelayMax).ShouldBeInRange(500, 899);
        }
    }

    [Fact]
    public void Random_WithMissingSetting_Returns1000()
    {
        _cache.Random(Account, AccountSettingEnums.TaskDelayMin, AccountSettingEnums.TaskDelayMax).ShouldBe(1000);
    }

    [Fact]
    public void Save_UpdatesDatabaseAndCache()
    {
        _cache.Get(Account, AccountSettingEnums.ClickDelayMin).ShouldBe(500);

        _cache.Save(Account, new() { [AccountSettingEnums.ClickDelayMin] = 700, [AccountSettingEnums.Tribe] = 3 });

        _cache.Get(Account, AccountSettingEnums.ClickDelayMin).ShouldBe(700);
        using var context = new AppDbContext(_options);
        context.AccountsSetting.Single(x => x.Setting == AccountSettingEnums.ClickDelayMin).Value.ShouldBe(700);
    }
}
