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
    }
}