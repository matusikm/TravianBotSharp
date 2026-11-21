using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Infrasturecture.Security;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MainCore.Test;

[Collection(nameof(SecretProtectionCollection))]
public sealed class DatabaseTests : IDisposable
{
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), "tbs-test-" + Guid.NewGuid().ToString("N"));
    private readonly ISecretProtector _previous;

    public DatabaseTests()
    {
        _previous = SecretProtection.Current;
        SecretProtection.Current = new AesGcmSecretProtector(Path.Combine(_keyDirectory, "secret.key"));
    }

    public void Dispose()
    {
        SecretProtection.Current = _previous;
        if (Directory.Exists(_keyDirectory)) Directory.Delete(_keyDirectory, recursive: true);
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static List<string> Schema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE '__EFMigrations%' ORDER BY name";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add(NormalizeSql(reader.GetString(0)));
        return result;
    }

    // ALTER TABLE ADD COLUMN appends to the stored CREATE text without line breaks, so compare
    // the statements with whitespace collapsed.
    private static string NormalizeSql(string sql)
    {
        var collapsed = Regex.Replace(sql, @"\s+", " ");
        return Regex.Replace(collapsed, @"\s*([(),])\s*", "$1");
    }

    /// <summary>A database as older versions left it: the first migration's schema and no migration history.</summary>
    private static void CreateLegacySchema(AppDbContext context)
    {
        var baseline = context.Database.GetMigrations().First();
        context.GetService<IMigrator>().Migrate(baseline);
        context.Database.ExecuteSqlRaw("DROP TABLE \"__EFMigrationsHistory\"");
    }

    private static string RawPassword(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Password FROM Accesses";
        return (string)command.ExecuteScalar()!;
    }

    [Fact]
    public void Migrations_MatchTheModel()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);

        context.Database.Migrate();

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public void Migrations_CreateTheSameSchemaAsEnsureCreated()
    {
        using var migrated = OpenConnection();
        using (var context = CreateContext(migrated)) context.Database.Migrate();

        using var created = OpenConnection();
        using (var context = CreateContext(created)) context.Database.EnsureCreated();

        Schema(migrated).ShouldBe(Schema(created));
    }

    [Fact]
    public void Initialize_UpgradesLegacyDatabase_AndEncryptsPlaintextPasswords()
    {
        using var connection = OpenConnection();
        using (var legacy = CreateContext(connection))
        {
            CreateLegacySchema(legacy);
            legacy.Database.ExecuteSqlRaw("""
                INSERT INTO Accounts (Id, Username, Server) VALUES (1, 'player', 'https://ts1.x1.europe.travian.com');
                INSERT INTO Accesses (Id, AccountId, Username, Password, ProxyHost, ProxyPort, ProxyUsername, ProxyPassword, Useragent, LastUsed)
                VALUES (1, 1, 'player', 'hunter2', 'proxy.example', 8080, 'alice', 'proxy-pass', '', '2026-01-01');
                """);
        }

        using (var context = CreateContext(connection))
        {
            DatabaseInitializer.Initialize(context);

            context.Database.GetAppliedMigrations().ShouldBe(context.Database.GetMigrations());
        }

        RawPassword(connection).ShouldStartWith(AesGcmSecretProtector.Prefix);

        using (var context = CreateContext(connection))
        {
            var access = context.Accesses.AsNoTracking().Single();
            access.Password.ShouldBe("hunter2");
            access.ProxyPassword.ShouldBe("proxy-pass");
            DatabaseInitializer.EncryptPlaintextSecrets(context).ShouldBe(0);
        }
    }

    [Fact]
    public void NewPasswords_AreStoredEncrypted()
    {
        using var connection = OpenConnection();
        using (var context = CreateContext(connection))
        {
            DatabaseInitializer.Initialize(context);
            context.Accounts.Add(new Account
            {
                Username = "player",
                Server = "https://ts1.x1.europe.travian.com",
                Accesses = [new Access { Username = "player", Password = "hunter2", ProxyHost = "", ProxyUsername = "", ProxyPassword = "", Useragent = "" }],
            });
            context.SaveChanges();
        }

        RawPassword(connection).ShouldStartWith(AesGcmSecretProtector.Prefix);

        using var reader = CreateContext(connection);
        reader.Accesses.Select(x => x.Password).Single().ShouldBe("hunter2");
    }
}

[CollectionDefinition(nameof(SecretProtectionCollection), DisableParallelization = true)]
public sealed class SecretProtectionCollection;
