using MainCore.UI.ViewModels.Abstract;
using MainCore.UI.ViewModels.UserControls;

namespace MainCore.UI.Models.Input
{
    public partial class AccountSettingInput : ViewModelBase
    {
        public void Set(Dictionary<AccountSettingEnums, int> settings)
        {
            Tribe.Set((TribeEnums)settings.GetValueOrDefault(AccountSettingEnums.Tribe));
            ClickDelay.Set(settings.GetValueOrDefault(AccountSettingEnums.ClickDelayMin), settings.GetValueOrDefault(AccountSettingEnums.ClickDelayMax));
            TaskDelay.Set(settings.GetValueOrDefault(AccountSettingEnums.TaskDelayMin), settings.GetValueOrDefault(AccountSettingEnums.TaskDelayMax));
            // Stored in minutes, shown in hours.
            WorkTime.Set(settings.GetValueOrDefault(AccountSettingEnums.WorkTimeMin) / 60, settings.GetValueOrDefault(AccountSettingEnums.WorkTimeMax) / 60);
            LogoutWhenWorkEnds = settings.GetValueOrDefault(AccountSettingEnums.WorkEndAction) == (int)WorkEndActionEnums.LogoutAndStop;
            EnableNightSleep = settings.GetValueOrDefault(AccountSettingEnums.EnableNightSleep) == 1;
            NightSleepStart = FormatTime(settings.GetValueOrDefault(AccountSettingEnums.NightSleepStart));
            NightSleepEnd = FormatTime(settings.GetValueOrDefault(AccountSettingEnums.NightSleepEnd));
            NightSleepJitter.Set(settings.GetValueOrDefault(AccountSettingEnums.NightSleepJitter));
            FarmIdle.Set(settings.GetValueOrDefault(AccountSettingEnums.FarmIdleMin), settings.GetValueOrDefault(AccountSettingEnums.FarmIdleMax));
            SleepTime.Set(settings.GetValueOrDefault(AccountSettingEnums.SleepTimeMin), settings.GetValueOrDefault(AccountSettingEnums.SleepTimeMax));
            EnableAutoLoadVillage = settings.GetValueOrDefault(AccountSettingEnums.EnableAutoLoadVillageBuilding) == 1;
            HeadlessChrome = settings.GetValueOrDefault(AccountSettingEnums.HeadlessChrome) == 1;
            EnableAutoStartAdventure = settings.GetValueOrDefault(AccountSettingEnums.EnableAutoStartAdventure) == 1;
            EnableAutoClaimDailyQuest = settings.GetValueOrDefault(AccountSettingEnums.EnableAutoClaimDailyQuest) == 1;
            EnableAutoSetHeroPoint = settings.GetValueOrDefault(AccountSettingEnums.EnableAutoSetHeroPoint) == 1;

            FarmInterval.Set(settings.GetValueOrDefault(AccountSettingEnums.FarmIntervalMin), settings.GetValueOrDefault(AccountSettingEnums.FarmIntervalMax));
            UseStartAllButton = settings.GetValueOrDefault(AccountSettingEnums.UseStartAllButton) == 1;

            EnableTelegram = settings.GetValueOrDefault(AccountSettingEnums.EnableTelegram) == 1;
            TelegramNotifyAttack = settings.GetValueOrDefault(AccountSettingEnums.TelegramNotifyAttack, 1) == 1;
            TelegramNotifyPaused = settings.GetValueOrDefault(AccountSettingEnums.TelegramNotifyPaused, 1) == 1;
            TelegramNotifyStorage = settings.GetValueOrDefault(AccountSettingEnums.TelegramNotifyStorage, 1) == 1;
        }

        public Dictionary<AccountSettingEnums, int> Get()
        {
            var tribe = (int)Tribe.Get();
            var (clickDelayMin, clickDelayMax) = ClickDelay.Get();
            var (taskDelayMin, taskDelayMax) = TaskDelay.Get();
            var isAutoLoadVillage = EnableAutoLoadVillage ? 1 : 0;
            var (workTimeMin, workTimeMax) = WorkTime.Get();
            workTimeMin *= 60;
            workTimeMax *= 60;
            var (farmIdleMin, farmIdleMax) = FarmIdle.Get();
            var (sleepTimeMin, sleepTimeMax) = SleepTime.Get();
            var headlessChrome = HeadlessChrome ? 1 : 0;
            var autoStartAdventure = EnableAutoStartAdventure ? 1 : 0;
            var autoClaimDailyQuest = EnableAutoClaimDailyQuest ? 1 : 0;
            var enableAutoSetHeroPoint = EnableAutoSetHeroPoint ? 1 : 0;

            var (farmIntervalMin, farmIntervalMax) = FarmInterval.Get();
            var useStartAllButton = UseStartAllButton ? 1 : 0;

            var settings = new Dictionary<AccountSettingEnums, int>()
            {
                { AccountSettingEnums.ClickDelayMin, clickDelayMin },
                { AccountSettingEnums.ClickDelayMax, clickDelayMax },
                { AccountSettingEnums.TaskDelayMin, taskDelayMin },
                { AccountSettingEnums.TaskDelayMax, taskDelayMax },
                { AccountSettingEnums.EnableAutoLoadVillageBuilding, isAutoLoadVillage },

                { AccountSettingEnums.FarmIntervalMin, farmIntervalMin },
                { AccountSettingEnums.FarmIntervalMax, farmIntervalMax },
                { AccountSettingEnums.UseStartAllButton, useStartAllButton },

                { AccountSettingEnums.Tribe, tribe },
                { AccountSettingEnums.WorkTimeMax, workTimeMax },
                { AccountSettingEnums.WorkTimeMin, workTimeMin },
                { AccountSettingEnums.SleepTimeMax, sleepTimeMax },
                { AccountSettingEnums.SleepTimeMin, sleepTimeMin },

                { AccountSettingEnums.HeadlessChrome, headlessChrome },
                { AccountSettingEnums.EnableAutoStartAdventure, autoStartAdventure },
                { AccountSettingEnums.EnableAutoClaimDailyQuest, autoClaimDailyQuest },
                { AccountSettingEnums.EnableAutoSetHeroPoint, enableAutoSetHeroPoint },

                { AccountSettingEnums.WorkEndAction, (int)(LogoutWhenWorkEnds ? WorkEndActionEnums.LogoutAndStop : WorkEndActionEnums.SleepAndContinue) },
                { AccountSettingEnums.EnableNightSleep, EnableNightSleep ? 1 : 0 },
                { AccountSettingEnums.NightSleepJitter, NightSleepJitter.Get() },
                { AccountSettingEnums.FarmIdleMin, farmIdleMin },
                { AccountSettingEnums.FarmIdleMax, farmIdleMax },

                { AccountSettingEnums.EnableTelegram, EnableTelegram ? 1 : 0 },
                { AccountSettingEnums.TelegramNotifyAttack, TelegramNotifyAttack ? 1 : 0 },
                { AccountSettingEnums.TelegramNotifyPaused, TelegramNotifyPaused ? 1 : 0 },
                { AccountSettingEnums.TelegramNotifyStorage, TelegramNotifyStorage ? 1 : 0 },
            };

            // Times that don't parse are caught by the validator; skip them here rather than save garbage.
            if (TryParseTime(NightSleepStart, out var nightStart)) settings[AccountSettingEnums.NightSleepStart] = nightStart;
            if (TryParseTime(NightSleepEnd, out var nightEnd)) settings[AccountSettingEnums.NightSleepEnd] = nightEnd;
            return settings;
        }

        public static string FormatTime(int minutesAfterMidnight)
        {
            var minutes = ((minutesAfterMidnight % 1440) + 1440) % 1440;
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        /// <summary>Parses "HH:mm" (24-hour) into minutes after midnight.</summary>
        public static bool TryParseTime(string? text, out int minutesAfterMidnight)
        {
            minutesAfterMidnight = 0;
            if (!TimeOnly.TryParseExact(text?.Trim(), ["H:mm", "HH:mm"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time))
            {
                return false;
            }
            minutesAfterMidnight = time.Hour * 60 + time.Minute;
            return true;
        }

        public TribeSelectorViewModel Tribe { get; } = new();
        public RangeInputViewModel ClickDelay { get; } = new();
        public RangeInputViewModel TaskDelay { get; } = new();

        public RangeInputViewModel WorkTime { get; } = new();
        public RangeInputViewModel SleepTime { get; } = new();
        public RangeInputViewModel FarmInterval { get; } = new();
        public RangeInputViewModel FarmIdle { get; } = new();
        public AmountInputViewModel NightSleepJitter { get; } = new();

        [Reactive]
        private bool _logoutWhenWorkEnds;

        [Reactive]
        private bool _enableNightSleep;

        [Reactive]
        private string _nightSleepStart = "23:30";

        [Reactive]
        private string _nightSleepEnd = "07:00";

        /// <summary>Stored on the account, not in the settings list. Empty: auto from the current IP.</summary>
        [Reactive]
        private string _timeZone = "";

        /// <summary>Stored on the account, not in the settings list. Empty: auto from the current IP.</summary>
        [Reactive]
        private string _language = "";

        [Reactive]
        private bool _enableAutoLoadVillage;

        [Reactive]
        private bool _headlessChrome;

        [Reactive]
        private bool _enableAutoStartAdventure;

        [Reactive]
        private bool _useStartAllButton;

        [Reactive]
        private bool _enableAutoClaimDailyQuest;

        [Reactive]
        private bool _enableAutoSetHeroPoint;

        [Reactive]
        private bool _enableTelegram;

        [Reactive]
        private bool _telegramNotifyAttack = true;

        [Reactive]
        private bool _telegramNotifyPaused = true;

        [Reactive]
        private bool _telegramNotifyStorage = true;

        /// <summary>Stored on the account (encrypted), not in the settings list or in exports.</summary>
        [Reactive]
        private string _telegramBotToken = "";

        /// <summary>Stored on the account, not in the settings list or in exports.</summary>
        [Reactive]
        private string _telegramChatId = "";
    }
}