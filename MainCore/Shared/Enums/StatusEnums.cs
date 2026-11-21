using Splat;

namespace MainCore.Shared.Enums
{
    public enum StatusEnums
    {
        Offline,
        Starting,
        Online,
        Pausing,
        Paused,
        Stopping,

        /// <summary>The internet is down; the bot checks it regularly and resumes on its own when it is back.</summary>
        WaitingForNetwork,
    }

    public static class StatusExtension
    {
        public static SplatColor GetColor(this StatusEnums status)
        {
            return status switch
            {
                StatusEnums.Online => SplatColor.Green,
                StatusEnums.Starting => SplatColor.Orange,
                StatusEnums.Pausing => SplatColor.Orange,
                StatusEnums.Stopping => SplatColor.Orange,
                StatusEnums.Offline => SplatColor.Black,
                StatusEnums.Paused => SplatColor.Red,
                StatusEnums.WaitingForNetwork => SplatColor.Purple,
                _ => SplatColor.Black,
            };
        }
    }
}