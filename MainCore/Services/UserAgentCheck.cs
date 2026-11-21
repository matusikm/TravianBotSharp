using System.Text.RegularExpressions;

namespace MainCore.Services;

/// <summary>
/// A custom User-Agent only changes the UA string. navigator.userAgentData and the Sec-CH-UA headers
/// still report the installed Chrome, so a different major version is an easy mismatch to spot.
/// </summary>
public static partial class UserAgentCheck
{
    [GeneratedRegex(@"Chrome/(\d+)\.", RegexOptions.CultureInvariant)]
    private static partial Regex ChromeVersion();

    public static int? ChromeMajorVersion(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return null;
        var match = ChromeVersion().Match(userAgent);
        return match.Success && int.TryParse(match.Groups[1].Value, out var major) ? major : null;
    }

    /// <summary>True when the User-Agent's Chrome major version matches the installed one, or either is unknown.</summary>
    public static bool Matches(string? userAgent, string? installedVersion)
    {
        var claimed = ChromeMajorVersion(userAgent);
        if (claimed is null || string.IsNullOrEmpty(installedVersion)) return true;

        var dot = installedVersion.IndexOf('.');
        var installedMajorText = dot < 0 ? installedVersion : installedVersion[..dot];
        return !int.TryParse(installedMajorText, out var installed) || installed == claimed;
    }
}
