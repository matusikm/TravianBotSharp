namespace MainCore.Shared.Enums
{
    public enum AccountSettingEnums
    {
        ClickDelayMin = 1,
        ClickDelayMax,
        TaskDelayMin,
        TaskDelayMax,
        EnableAutoLoadVillageBuilding,
        UseStartAllButton,
        FarmIntervalMin,
        FarmIntervalMax,
        Tribe,
        WorkTimeMin,
        WorkTimeMax,
        SleepTimeMin,
        SleepTimeMax,
        HeadlessChrome,
        EnableAutoStartAdventure,
        EnableAutoClaimDailyQuest,
        EnableAutoSetHeroPoint,

        /// <summary>What happens when work time ends: 0 = sleep and continue, 1 = log out and stop.</summary>
        WorkEndAction,

        /// <summary>1 = always sleep during the night window, in the account's time zone.</summary>
        EnableNightSleep,

        /// <summary>Night window start, in minutes after midnight.</summary>
        NightSleepStart,

        /// <summary>Night window end, in minutes after midnight.</summary>
        NightSleepEnd,

        /// <summary>Random shift of the night window each day, in minutes either way.</summary>
        NightSleepJitter,

        /// <summary>Idle gap between farming bursts, in minutes.</summary>
        FarmIdleMin,

        FarmIdleMax,

        /// <summary>1 = send Telegram notifications for this account (bot token and chat are stored on the account).</summary>
        EnableTelegram,

        /// <summary>1 = Telegram message when a village gets attacked.</summary>
        TelegramNotifyAttack,

        /// <summary>1 = Telegram message when the bot pauses because of an error or a long internet outage.</summary>
        TelegramNotifyPaused,

        /// <summary>1 = Telegram message when a village's warehouse or granary is nearly full.</summary>
        TelegramNotifyStorage,
    }
}