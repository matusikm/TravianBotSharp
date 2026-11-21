using System.Globalization;
using System.Text.Json;

namespace MainCore.Services;

/// <summary>What an IP geolocation lookup reported. Any field can be missing.</summary>
public sealed record GeoInfo(string? TimeZone, string? CountryCode, string? Languages);

/// <summary>The time zone and language an account's browser should report.</summary>
public sealed record ResolvedLocale(TimeZoneInfo TimeZone, string TimeZoneId, string? Locale);

/// <summary>
/// Picks the browser's time zone and language for an account: a fixed value from the settings wins,
/// otherwise the current IP's location, otherwise this machine's own zone and Chrome's default language.
/// </summary>
public static class AccountLocale
{
    public static ResolvedLocale Resolve(string? fixedTimeZone, string? fixedLanguage, GeoInfo? geo, TimeZoneInfo machineZone)
    {
        var zone = FindZone(fixedTimeZone) ?? FindZone(geo?.TimeZone) ?? machineZone;
        var language = IsValidLanguage(fixedLanguage) ? fixedLanguage : LanguageFromGeo(geo);
        return new ResolvedLocale(zone, ToIanaId(zone), language);
    }

    /// <summary>True when the account needs an IP lookup, because its time zone or language is left on auto.</summary>
    public static bool NeedsLookup(string? fixedTimeZone, string? fixedLanguage) =>
        FindZone(fixedTimeZone) is null || !IsValidLanguage(fixedLanguage);

    public static TimeZoneInfo? FindZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var zone) ? zone : null;
    }

    public static bool IsValidLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return false;
        try
        {
            var culture = CultureInfo.GetCultureInfo(language.Trim(), predefinedOnly: true);
            return !culture.Equals(CultureInfo.InvariantCulture);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// A language tag such as th-TH: the country's first listed language when the lookup gave one,
    /// otherwise the first culture .NET knows for that country.
    /// </summary>
    public static string? LanguageFromGeo(GeoInfo? geo)
    {
        var country = geo?.CountryCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(country) || country.Length != 2) return null;

        var first = geo!.Languages?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (!string.IsNullOrEmpty(first))
        {
            var tag = first.Contains('-') ? first : $"{first}-{country}";
            if (IsValidLanguage(tag)) return CultureInfo.GetCultureInfo(tag).Name;
        }

        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Where(c => c.Name.EndsWith($"-{country}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name.Length)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => c.Name)
            .FirstOrDefault();
    }

    /// <summary>Chrome wants IANA ids (Europe/Prague); Windows zones use their own names.</summary>
    public static string ToIanaId(TimeZoneInfo zone)
    {
        if (zone.HasIanaId) return zone.Id;
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
    }

    /// <summary>Reads the JSON from ipapi.co or ipinfo.io into a <see cref="GeoInfo"/>.</summary>
    public static GeoInfo? ParseGeoJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.True) return null;

            var timeZone = Text(root, "timezone");
            var country = Text(root, "country_code") ?? Text(root, "country");
            var languages = Text(root, "languages");
            return timeZone is null && country is null ? null : new GeoInfo(timeZone, country, languages);
        }
        catch (JsonException)
        {
            return null;
        }

        static string? Text(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()
                : null;
    }
}
