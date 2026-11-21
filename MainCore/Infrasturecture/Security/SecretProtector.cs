using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

using Serilog;

namespace MainCore.Infrasturecture.Security;

/// <summary>
/// Encrypts secrets (Travian and proxy passwords) before they are written to TBS.db.
/// Protected values carry a scheme prefix so plaintext rows from older versions can be told apart.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/>. Empty values and already protected values are returned unchanged.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypts a protected value. Plaintext from older versions is returned unchanged.</summary>
    string Unprotect(string stored);
}

/// <summary>
/// The protector used by the EF Core value converters. EF caches the model per context type,
/// so the converters read it from here instead of from DI.
/// </summary>
public static class SecretProtection
{
    private static readonly Lock _gate = new();
    private static ISecretProtector? _current;

    public static ISecretProtector Current
    {
        get
        {
            if (_current is not null) return _current;
            lock (_gate)
            {
                return _current ??= CreateDefault();
            }
        }
        set
        {
            lock (_gate)
            {
                _current = value;
            }
        }
    }

    public static string Protect(string value) => Current.Protect(value);

    public static string Unprotect(string value) => Current.Unprotect(value);

    /// <summary>True when the stored value was written by any protector (has a scheme prefix).</summary>
    public static bool IsProtected(string? stored) =>
        !string.IsNullOrEmpty(stored)
        && (stored.StartsWith(DpapiSecretProtector.Prefix, StringComparison.Ordinal)
            || stored.StartsWith(AesGcmSecretProtector.Prefix, StringComparison.Ordinal));

    /// <summary>
    /// Windows: DPAPI tied to the Windows user. Elsewhere: AES-GCM with a key kept outside the
    /// app folder (~/.config/TravianBotSharp/secret.key, owner-only), so a copied TBS.db or app
    /// folder doesn't carry the passwords with it.
    /// </summary>
    public static ISecretProtector CreateDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiSecretProtector();
        }

        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
        return new AesGcmSecretProtector(Path.Combine(configDir, "TravianBotSharp", "secret.key"));
    }
}

/// <summary>DPAPI for the current Windows user. Only constructed on Windows.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    public const string Prefix = "dpapi:v1:";

    private static readonly byte[] Entropy = "TravianBotSharp.Secrets"u8.ToArray();

    [SupportedOSPlatform("windows")]
    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || SecretProtection.IsProtected(plaintext)) return plaintext;

        var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(cipher);
    }

    [SupportedOSPlatform("windows")]
    public string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;

        try
        {
            var plain = ProtectedData.Unprotect(Convert.FromBase64String(stored[Prefix.Length..]), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            Log.Warning(ex, "A stored password could not be decrypted (was TBS.db copied from another Windows user?). Enter it again.");
            return "";
        }
    }
}

public sealed class AesGcmSecretProtector : ISecretProtector
{
    public const string Prefix = "aesgcm:v1:";

    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string _keyPath;
    private readonly Lazy<byte[]> _key;

    public AesGcmSecretProtector(string keyPath)
    {
        _keyPath = keyPath;
        _key = new Lazy<byte[]>(LoadOrCreateKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || SecretProtection.IsProtected(plaintext)) return plaintext;

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key.Value, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;

        try
        {
            var data = Convert.FromBase64String(stored[Prefix.Length..]);
            if (data.Length < NonceSize + TagSize) throw new CryptographicException("Protected value is too short.");

            var nonce = data.AsSpan(0, NonceSize);
            var tag = data.AsSpan(NonceSize, TagSize);
            var cipher = data.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using (var aes = new AesGcm(_key.Value, TagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            Log.Warning(ex, "A stored password could not be decrypted (was {KeyPath} deleted or TBS.db copied from another machine?). Enter it again.", _keyPath);
            return "";
        }
    }

    private byte[] LoadOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            var existing = File.ReadAllBytes(_keyPath);
            if (existing.Length == KeySize) return existing;
            throw new CryptographicException($"The key file {_keyPath} is damaged. Delete it and enter the passwords again.");
        }

        var directory = Path.GetDirectoryName(_keyPath)!;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using var stream = new FileStream(_keyPath, options);
            stream.Write(key);
            return key;
        }
        catch (IOException) when (File.Exists(_keyPath))
        {
            // Another process created it first; use theirs.
            return File.ReadAllBytes(_keyPath);
        }
    }
}
