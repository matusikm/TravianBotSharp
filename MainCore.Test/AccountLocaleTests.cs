using MainCore.Services;
using MainCore.UI.Models.Input;
using MainCore.UI.Models.Validators;
using MainCore.Shared.Enums;

namespace MainCore.Test;

public sealed class AccountLocaleTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    [Fact]
    public void FixedValues_Win()
    {
        var geo = new GeoInfo("Asia/Bangkok", "TH", "th,en");
        var locale = AccountLocale.Resolve("Europe/Prague", "en-US", geo, Prague);

        locale.TimeZoneId.ShouldBe("Europe/Prague");
        locale.Locale.ShouldBe("en-US");
        AccountLocale.NeedsLookup("Europe/Prague", "en-US").ShouldBeFalse();
    }

    [Fact]
    public void Auto_UsesTheIpLocation()
    {
        var geo = new GeoInfo("Asia/Bangkok", "TH", "th,en");
        var locale = AccountLocale.Resolve("", null, geo, Prague);

        locale.TimeZoneId.ShouldBe("Asia/Bangkok");
        locale.TimeZone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(7));
        locale.Locale.ShouldBe("th-TH");
        AccountLocale.NeedsLookup("", null).ShouldBeTrue();
        AccountLocale.NeedsLookup("Europe/Prague", "").ShouldBeTrue();
    }

    [Fact]
    public void Auto_FallsBackToThisMachine_WhenTheLookupFails()
    {
        var locale = AccountLocale.Resolve(null, null, null, Prague);

        locale.TimeZoneId.ShouldBe("Europe/Prague");
        locale.Locale.ShouldBeNull();
    }

    [Fact]
    public void InvalidFixedValues_AreTreatedAsAuto()
    {
        var geo = new GeoInfo("Asia/Bangkok", "TH", null);
        var locale = AccountLocale.Resolve("Mars/Olympus", "xx-NOPE", geo, Prague);

        locale.TimeZoneId.ShouldBe("Asia/Bangkok");
        locale.Locale.ShouldBe("th-TH");
    }

    [Theory]
    [InlineData("CZ", null, "cs-CZ")]
    [InlineData("TH", null, "th-TH")]
    [InlineData("CZ", "cs,sk", "cs-CZ")]
    [InlineData("SG", "en-SG,ms-SG", "en-SG")]
    [InlineData("", null, null)]
    public void LanguageFromCountry(string country, string? languages, string? expected)
    {
        AccountLocale.LanguageFromGeo(new GeoInfo(null, country, languages)).ShouldBe(expected);
    }

    [Fact]
    public void ParsesIpinfoJson()
    {
        const string json = """{"ip":"203.0.113.7","city":"Bangkok","country":"TH","timezone":"Asia/Bangkok"}""";
        AccountLocale.ParseGeoJson(json).ShouldBe(new GeoInfo("Asia/Bangkok", "TH", null));
    }

    [Fact]
    public void ParsesIpapiJson()
    {
        const string json = """{"ip":"203.0.113.7","country":"CZ","country_code":"CZ","timezone":"Europe/Prague","languages":"cs,sk"}""";
        AccountLocale.ParseGeoJson(json).ShouldBe(new GeoInfo("Europe/Prague", "CZ", "cs,sk"));
    }

    [Theory]
    [InlineData("""{"error":true,"reason":"RateLimited"}""")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void BadGeoJson_GivesNothing(string json)
    {
        AccountLocale.ParseGeoJson(json).ShouldBeNull();
    }

    [Fact]
    public void SettingsInput_ShowsWorkTimeInHours_AndStoresMinutes()
    {
        var input = new AccountSettingInput();
        input.Set(new()
        {
            [AccountSettingEnums.WorkTimeMin] = 480,
            [AccountSettingEnums.WorkTimeMax] = 720,
            [AccountSettingEnums.WorkEndAction] = (int)WorkEndActionEnums.LogoutAndStop,
            [AccountSettingEnums.EnableNightSleep] = 1,
            [AccountSettingEnums.NightSleepStart] = 23 * 60 + 30,
            [AccountSettingEnums.NightSleepEnd] = 7 * 60,
            [AccountSettingEnums.NightSleepJitter] = 45,
            [AccountSettingEnums.FarmIdleMin] = 9,
            [AccountSettingEnums.FarmIdleMax] = 63,
        });

        input.WorkTime.Get().ShouldBe((8, 12));
        input.LogoutWhenWorkEnds.ShouldBeTrue();
        input.NightSleepStart.ShouldBe("23:30");
        input.NightSleepEnd.ShouldBe("07:00");

        input.NightSleepStart = "22:15";
        var settings = input.Get();
        settings[AccountSettingEnums.WorkTimeMin].ShouldBe(480);
        settings[AccountSettingEnums.WorkTimeMax].ShouldBe(720);
        settings[AccountSettingEnums.WorkEndAction].ShouldBe((int)WorkEndActionEnums.LogoutAndStop);
        settings[AccountSettingEnums.NightSleepStart].ShouldBe(22 * 60 + 15);
        settings[AccountSettingEnums.FarmIdleMax].ShouldBe(63);
    }

    [Fact]
    public void SettingsValidator_RejectsBadTimesZonesAndLanguages()
    {
        var input = new AccountSettingInput();
        input.Set(new()
        {
            [AccountSettingEnums.WorkTimeMin] = 480,
            [AccountSettingEnums.WorkTimeMax] = 720,
            [AccountSettingEnums.FarmIdleMin] = 9,
            [AccountSettingEnums.FarmIdleMax] = 63,
        });
        var validator = new AccountSettingInputValidator();
        validator.Validate(input).IsValid.ShouldBeTrue();

        input.NightSleepStart = "25:00";
        input.TimeZone = "Mars/Olympus";
        input.Language = "xx-NOPE";
        var errors = validator.Validate(input).Errors.Select(x => x.PropertyName).ToList();
        errors.ShouldContain(nameof(AccountSettingInput.NightSleepStart));
        errors.ShouldContain(nameof(AccountSettingInput.TimeZone));
        errors.ShouldContain(nameof(AccountSettingInput.Language));
    }
}
