using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using MainCore.Infrasturecture.Security;

namespace MainCore.Infrasturecture;

/// <summary>Brings TBS.db up to date on start: schema, encrypted secrets and default settings.</summary>
public static class DatabaseInitializer
{
    public static void Initialize(AppDbContext context)
    {
        BaselineLegacyDatabase(context);
        context.Database.Migrate();

        EncryptPlaintextSecrets(context);

        context.FillAccountSettings();
        context.FillVillageSettings();

        context.QueueBuildings
            .Where(x => x.Level == -1)
            .ExecuteDelete();

        RestrictFilePermissions(context);
    }

    /// <summary>
    /// Older versions created the schema with EnsureCreated, which leaves no migration history.
    /// Record the baseline as applied so Migrate doesn't try to create existing tables.
    /// </summary>
    public static void BaselineLegacyDatabase(AppDbContext context)
    {
        var database = context.Database;
        if (!database.CanConnect()) return;

        var history = database.GetService<IHistoryRepository>();
        if (history.Exists()) return;

        var hasSchema = database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = 'Accounts'")
            .AsEnumerable()
            .First() > 0;
        if (!hasSchema) return;

        // The first migration matches the schema EnsureCreated produced.
        var baseline = database.GetMigrations().First();
        database.ExecuteSqlRaw(history.GetCreateIfNotExistsScript());
        database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(baseline, ProductInfo.GetVersion())));
    }

    /// <summary>Re-saves passwords still stored as plaintext so the value converter encrypts them.</summary>
    public static int EncryptPlaintextSecrets(AppDbContext context)
    {
        var plaintext = context.Accesses
            .FromSqlRaw("""
                SELECT * FROM "Accesses"
                WHERE ("Password" <> '' AND "Password" NOT LIKE '%:v1:%')
                   OR ("ProxyPassword" <> '' AND "ProxyPassword" NOT LIKE '%:v1:%')
                """)
            .ToList();
        if (plaintext.Count == 0) return 0;

        foreach (var access in plaintext)
        {
            var entry = context.Entry(access);
            entry.Property(x => x.Password).IsModified = true;
            entry.Property(x => x.ProxyPassword).IsModified = true;
        }

        context.SaveChanges();
        return plaintext.Count;
    }

    /// <summary>On Linux and macOS, make the database and browser profiles readable by the owner only.</summary>
    public static void RestrictFilePermissions(AppDbContext context)
    {
        if (OperatingSystem.IsWindows()) return;

        const UnixFileMode ownerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const UnixFileMode ownerDirectory = ownerFile | UnixFileMode.UserExecute;

        var dbPath = context.Database.GetDbConnection().DataSource;
        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
        {
            File.SetUnixFileMode(dbPath, ownerFile);
        }

        var dataDirectory = Path.Combine(AppPaths.DataDirectory, "Data");
        if (Directory.Exists(dataDirectory))
        {
            File.SetUnixFileMode(dataDirectory, ownerDirectory);
        }
    }
}
