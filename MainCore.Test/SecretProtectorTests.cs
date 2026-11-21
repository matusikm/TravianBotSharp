using MainCore.Infrasturecture.Security;

namespace MainCore.Test;

public sealed class SecretProtectorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tbs-test-" + Guid.NewGuid().ToString("N"));
    private string KeyPath => Path.Combine(_directory, "secret.key");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void RoundTrips_AndUsesFreshNonce()
    {
        var protector = new AesGcmSecretProtector(KeyPath);

        var first = protector.Protect("hunter2");
        var second = protector.Protect("hunter2");

        first.ShouldStartWith(AesGcmSecretProtector.Prefix);
        first.ShouldNotContain("hunter2");
        first.ShouldNotBe(second);
        protector.Unprotect(first).ShouldBe("hunter2");
    }

    [Fact]
    public void KeyFile_IsReusedAndOwnerOnly()
    {
        var stored = new AesGcmSecretProtector(KeyPath).Protect("hunter2");

        new AesGcmSecretProtector(KeyPath).Unprotect(stored).ShouldBe("hunter2");

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(KeyPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("aesgcm:v1:already")]
    public void Protect_LeavesEmptyAndProtectedValuesAlone(string value)
    {
        new AesGcmSecretProtector(KeyPath).Protect(value).ShouldBe(value);
    }

    [Fact]
    public void Unprotect_ReturnsLegacyPlaintextUnchanged()
    {
        new AesGcmSecretProtector(KeyPath).Unprotect("hunter2").ShouldBe("hunter2");
    }

    [Fact]
    public void Unprotect_WithDifferentKey_ReturnsEmpty()
    {
        var stored = new AesGcmSecretProtector(KeyPath).Protect("hunter2");
        var other = new AesGcmSecretProtector(Path.Combine(_directory, "other.key"));

        other.Unprotect(stored).ShouldBe("");
    }
}
